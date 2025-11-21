const encoder = new TextEncoder();

function toBase64(bytes) {
    if (!(bytes instanceof Uint8Array)) {
        bytes = new Uint8Array(bytes);
    }

    let binary = "";
    const chunkSize = 0x8000;
    for (let i = 0; i < bytes.length; i += chunkSize) {
        const chunk = bytes.subarray(i, i + chunkSize);
        binary += String.fromCharCode.apply(null, chunk);
    }

    return btoa(binary);
}

export async function generateOfflineKeyBundle(passphrase, tenantLabel) {
    if (!passphrase || passphrase.length < 12) {
        throw new Error("Passphrase must be at least 12 characters.");
    }

    const dek = crypto.getRandomValues(new Uint8Array(32));
    const salt = crypto.getRandomValues(new Uint8Array(16));
    const nonce = crypto.getRandomValues(new Uint8Array(12));

    const passphraseKey = await crypto.subtle.importKey(
        "raw",
        encoder.encode(passphrase),
        { name: "PBKDF2" },
        false,
        ["deriveKey"]
    );

    const wrappingKey = await crypto.subtle.deriveKey(
        {
            name: "PBKDF2",
            salt,
            iterations: 310000,
            hash: "SHA-256"
        },
        passphraseKey,
        { name: "AES-GCM", length: 256 },
        false,
        ["encrypt", "decrypt"]
    );

    const wrappedDekBuffer = await crypto.subtle.encrypt(
        { name: "AES-GCM", iv: nonce },
        wrappingKey,
        dek
    );

    const checksumBuffer = await crypto.subtle.digest("SHA-256", dek);

    return {
        version: "offline-byok/v1",
        tenantLabel: tenantLabel || "Unnamed Tenant",
        createdAt: new Date().toISOString(),
        plaintextDek: toBase64(dek),
        wrappedDek: toBase64(new Uint8Array(wrappedDekBuffer)),
        salt: toBase64(salt),
        nonce: toBase64(nonce),
        checksum: toBase64(new Uint8Array(checksumBuffer))
    };
}

export function downloadTextFile(fileName, content) {
    const blob = new Blob([content], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
}


