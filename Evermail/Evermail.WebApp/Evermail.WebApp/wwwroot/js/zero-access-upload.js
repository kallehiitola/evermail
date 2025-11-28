(() => {
    const keyCache = new Map();
    const encoder = new TextEncoder();

    const toBase64 = (bytes) => btoa(String.fromCharCode(...bytes));
    const fromBase64 = (value) => Uint8Array.from(atob(value), c => c.charCodeAt(0));

    async function generateBundle() {
        const cryptoKey = await crypto.subtle.generateKey(
            { name: 'AES-GCM', length: 256 },
            true,
            ['encrypt', 'decrypt']
        );
        const rawKey = new Uint8Array(await crypto.subtle.exportKey('raw', cryptoKey));
        const fingerprintBuffer = await crypto.subtle.digest('SHA-256', rawKey);
        const fingerprint = toBase64(new Uint8Array(fingerprintBuffer));
        keyCache.set(fingerprint, cryptoKey);

        const noncePrefix = new Uint8Array(8);
        crypto.getRandomValues(noncePrefix);

        return {
            keyBase64: toBase64(rawKey),
            fingerprint,
            noncePrefixBase64: toBase64(noncePrefix),
            scheme: 'zero-access/aes-gcm-chunked/v1'
        };
    }

    async function ensureKey(bundle) {
        if (keyCache.has(bundle.fingerprint)) {
            return keyCache.get(bundle.fingerprint);
        }

        const rawKey = fromBase64(bundle.keyBase64);
        const imported = await crypto.subtle.importKey(
            'raw',
            rawKey,
            { name: 'AES-GCM' },
            false,
            ['encrypt']
        );
        keyCache.set(bundle.fingerprint, imported);
        return imported;
    }

    function buildNonce(prefix, chunkIndex) {
        const nonce = new Uint8Array(12);
        nonce.set(prefix, 0);
        const view = new DataView(nonce.buffer);
        view.setUint32(8, chunkIndex, false);
        return nonce;
    }

    function concatBuffers(...arrays) {
        const totalLength = arrays.reduce((sum, arr) => sum + arr.length, 0);
        const result = new Uint8Array(totalLength);
        let offset = 0;
        for (const arr of arrays) {
            result.set(arr, offset);
            offset += arr.length;
        }
        return result;
    }

    async function encryptAndUpload(sasUrl, dotNetRef, options) {
        const fileInput = document.getElementById('fileInput');
        if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
            throw new Error('No file selected');
        }

        const file = fileInput.files[0];
        const chunkSize = options?.chunkSize || (2 * 1024 * 1024);
        const totalChunks = Math.ceil(file.size / chunkSize);
        const blockIds = [];
        const startTime = Date.now();

        const bundle = {
            keyBase64: options.keyBase64,
            fingerprint: options.fingerprint,
            noncePrefixBase64: options.noncePrefixBase64
        };

        const cryptoKey = await ensureKey(bundle);
        const noncePrefix = fromBase64(bundle.noncePrefixBase64);

        const metadata = {
            scheme: options.scheme,
            originalSizeBytes: file.size,
            cipherSizeBytes: 0,
            chunkSize,
            totalChunks,
            chunkPlainSizes: [],
            noncePrefix: bundle.noncePrefixBase64,
            tagLength: 16,
            generatedAt: new Date().toISOString(),
            fingerprint: bundle.fingerprint
        };

        let uploadedBytes = 0;

        for (let i = 0; i < totalChunks; i++) {
            const start = i * chunkSize;
            const end = Math.min(start + chunkSize, file.size);
            const chunk = file.slice(start, end);
            const chunkBuffer = await chunk.arrayBuffer();
            const nonce = buildNonce(noncePrefix, i);
            const cipherBuffer = await crypto.subtle.encrypt(
                { name: 'AES-GCM', iv: nonce },
                cryptoKey,
                chunkBuffer
            );
            const payload = concatBuffers(nonce, new Uint8Array(cipherBuffer));

            const blockId = btoa(`block-${String(i).padStart(10, '0')}`);
            blockIds.push(blockId);

            const blockUrl = `${sasUrl}&comp=block&blockid=${encodeURIComponent(blockId)}`;
            const uploadResponse = await fetch(blockUrl, {
                method: 'PUT',
                body: payload,
                headers: {
                    'x-ms-blob-type': 'BlockBlob',
                    'Content-Length': payload.length.toString()
                }
            });

            if (!uploadResponse.ok) {
                throw new Error(`Encrypted block upload failed: ${uploadResponse.status} ${uploadResponse.statusText}`);
            }

            metadata.chunkPlainSizes.push(chunk.size);
            metadata.cipherSizeBytes += payload.length;
            uploadedBytes += chunk.size;

            const progress = Math.round((uploadedBytes / file.size) * 100);
            const elapsedSeconds = (Date.now() - startTime) / 1000;
            const speedMBps = (uploadedBytes / (1024 * 1024)) / Math.max(elapsedSeconds, 0.001);
            dotNetRef.invokeMethodAsync('UpdateProgress', progress, uploadedBytes, speedMBps);
        }

        const commitUrl = `${sasUrl}&comp=blocklist`;
        const blockListXml = `<?xml version="1.0" encoding="utf-8"?><BlockList>${blockIds.map(id => `<Latest>${id}</Latest>`).join('')}</BlockList>`;
        const commitResponse = await fetch(commitUrl, {
            method: 'PUT',
            body: blockListXml,
            headers: {
                'Content-Type': 'application/xml',
                'Content-Length': blockListXml.length.toString()
            }
        });

        if (!commitResponse.ok) {
            throw new Error(`Commit failed: ${commitResponse.status} ${commitResponse.statusText}`);
        }

        dotNetRef.invokeMethodAsync('UpdateProgress', 100, file.size, 0);

        return {
            metadataJson: JSON.stringify(metadata),
            cipherSizeBytes: metadata.cipherSizeBytes
        };
    }

    function ensureFileInput() {
        const fileInput = document.getElementById('fileInput');
        if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
            throw new Error('No file selected');
        }
        return fileInput.files[0];
    }

    function isMboxLikeFile(file) {
        const name = (file.name || '').toLowerCase();
        return name.endsWith('.mbox') || name.endsWith('.mbx');
    }

    function normalizeSubject(value) {
        if (!value) {
            return '';
        }
        return value
            .toLowerCase()
            .replace(/\s+/g, ' ')
            .trim()
            .slice(0, 200);
    }

    function extractAddresses(value) {
        if (!value) {
            return [];
        }
        const matches = value.match(/[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/g);
        if (!matches) {
            return [];
        }
        return matches
            .map(addr => addr.toLowerCase())
            .map(addr => addr.trim())
            .filter(Boolean);
    }

    async function deriveDeterministicTokens(bundle, tokenSaltBase64, items) {
        if (!bundle || !bundle.keyBase64 || !tokenSaltBase64 || !Array.isArray(items) || items.length === 0) {
            return [];
        }

        const rawKey = fromBase64(bundle.keyBase64);
        const salt = fromBase64(tokenSaltBase64);
        const info = encoder.encode('evermail-zero-access-token/v1');

        const hkdfKey = await crypto.subtle.importKey(
            'raw',
            rawKey,
            'HKDF',
            false,
            ['deriveKey']
        );

        const hmacKey = await crypto.subtle.deriveKey(
            {
                name: 'HKDF',
                hash: 'SHA-256',
                salt,
                info
            },
            hkdfKey,
            {
                name: 'HMAC',
                hash: 'SHA-256',
                length: 256
            },
            true,
            ['sign']
        );

        const results = [];
        for (const originalValue of items) {
            const normalized = (originalValue ?? '').trim().toLowerCase();
            if (!normalized) {
                continue;
            }

            const signature = await crypto.subtle.sign(
                'HMAC',
                hmacKey,
                encoder.encode(normalized)
            );

            results.push(toBase64(new Uint8Array(signature)));
        }

        return results;
    }

    async function deriveTagTokens(bundle, tokenSaltBase64, tags) {
        return deriveDeterministicTokens(bundle, tokenSaltBase64, tags);
    }

    async function scanHeaderTokens(options) {
        let file;
        try {
            file = ensureFileInput();
        } catch {
            return { success: false, reason: 'no-file' };
        }

        if (!isMboxLikeFile(file)) {
            return { success: false, reason: 'unsupported-format' };
        }

        const limits = {
            maxMessages: options?.maxMessages ?? 2000,
            maxTokensPerType: options?.maxTokensPerType ?? 512
        };

        return await parseMboxHeaders(file, limits);
    }

    async function parseMboxHeaders(file, limits) {
        const reader = file.stream().getReader();
        const decoder = new TextDecoder('utf-8');
        const sets = {
            from: new Set(),
            to: new Set(),
            cc: new Set(),
            subject: new Set()
        };
        const capped = {
            from: false,
            to: false,
            cc: false,
            subject: false
        };

        const addValue = (type, value) => {
            if (!value) {
                return;
            }
            if (sets[type].has(value)) {
                return;
            }
            if (sets[type].size >= limits.maxTokensPerType) {
                capped[type] = true;
                return;
            }
            sets[type].add(value);
        };

        let leftover = '';
        let inHeaders = false;
        let currentHeaders = [];
        let messageCount = 0;
        let shouldStop = false;

        const finalizeHeaders = () => {
            if (currentHeaders.length === 0) {
                inHeaders = false;
                currentHeaders = [];
                return;
            }

            messageCount += 1;
            const unfolded = [];
            for (const line of currentHeaders) {
                if (/^\s/.test(line) && unfolded.length > 0) {
                    unfolded[unfolded.length - 1] += ` ${line.trim()}`;
                } else {
                    unfolded.push(line);
                }
            }

            const headerMap = {};
            for (const line of unfolded) {
                const separatorIndex = line.indexOf(':');
                if (separatorIndex < 0) {
                    continue;
                }
                const name = line.slice(0, separatorIndex).trim().toLowerCase();
                const value = line.slice(separatorIndex + 1).trim();
                if (!headerMap[name]) {
                    headerMap[name] = [];
                }
                headerMap[name].push(value);
            }

            if (headerMap['from']) {
                for (const entry of headerMap['from']) {
                    extractAddresses(entry).forEach(addr => addValue('from', addr));
                }
            }
            if (headerMap['to']) {
                for (const entry of headerMap['to']) {
                    extractAddresses(entry).forEach(addr => addValue('to', addr));
                }
            }
            if (headerMap['cc']) {
                for (const entry of headerMap['cc']) {
                    extractAddresses(entry).forEach(addr => addValue('cc', addr));
                }
            }
            if (headerMap['subject'] && headerMap['subject'][0]) {
                const normalizedSubject = normalizeSubject(headerMap['subject'][0]);
                if (normalizedSubject) {
                    addValue('subject', normalizedSubject);
                }
            }

            currentHeaders = [];
            inHeaders = false;
        };

        try {
            while (true) {
                const { value, done } = await reader.read();
                if (done) {
                    leftover += decoder.decode();
                    break;
                }

                leftover += decoder.decode(value, { stream: true });
                let newlineIndex;

                while ((newlineIndex = leftover.indexOf('\n')) >= 0) {
                    let line = leftover.slice(0, newlineIndex);
                    leftover = leftover.slice(newlineIndex + 1);
                    if (line.endsWith('\r')) {
                        line = line.slice(0, -1);
                    }

                    if (line.startsWith('From ') && !line.startsWith('From :')) {
                        if (inHeaders) {
                            finalizeHeaders();
                        }
                        if (messageCount >= limits.maxMessages) {
                            shouldStop = true;
                            break;
                        }
                        inHeaders = true;
                        currentHeaders = [];
                        continue;
                    }

                    if (inHeaders) {
                        if (line.length === 0) {
                            finalizeHeaders();
                            if (messageCount >= limits.maxMessages) {
                                shouldStop = true;
                                break;
                            }
                        } else {
                            currentHeaders.push(line);
                        }
                    }
                }

                if (shouldStop) {
                    break;
                }
            }
        } finally {
            try {
                await reader.cancel();
            } catch {
                // ignore cancellation errors
            }
        }

        if (inHeaders && !shouldStop) {
            finalizeHeaders();
        }

        return {
            success: true,
            values: {
                from: Array.from(sets.from),
                to: Array.from(sets.to),
                cc: Array.from(sets.cc),
                subject: Array.from(sets.subject)
            },
            stats: {
                scannedMessages: messageCount,
                cappedTypes: capped
            }
        };
    }

    async function hashHeaderTokens(bundle, tokenSaltBase64, headerMap) {
        if (!bundle || !tokenSaltBase64 || !headerMap) {
            return {};
        }

        const result = {};
        for (const [type, values] of Object.entries(headerMap)) {
            if (!Array.isArray(values) || values.length === 0) {
                continue;
            }
            const hashed = await deriveDeterministicTokens(bundle, tokenSaltBase64, values);
            if (hashed.length > 0) {
                result[type] = hashed;
            }
        }
        return result;
    }

    function downloadBundle(bundle) {
        const blob = new Blob([JSON.stringify(bundle, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `evermail-zero-access-${bundle.fingerprint.slice(0, 8)}.json`;
        anchor.click();
        URL.revokeObjectURL(url);
    }

    async function copyToClipboard(value) {
        try {
            await navigator.clipboard.writeText(value);
        } catch (error) {
            console.warn('Clipboard copy failed', error);
        }
    }

    window.zeroAccessUpload = {
        generateBundle,
        encryptAndUpload,
        downloadBundle,
        copyToClipboard,
        deriveTagTokens,
        scanHeaderTokens,
        hashHeaderTokens
    };
})();


