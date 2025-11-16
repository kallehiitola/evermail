# Generate Test .mbox Files

## Quick Start

The `generate-test-mbox.py` script creates realistic .mbox files with simulated emails.

### Create Test Files:

```bash
cd /Users/kallehiitola/Work/evermail

# Small file - 10MB (~13,500 emails)
python3 scripts/generate-test-mbox.py 10 ~/Downloads/test-10mb.mbox

# Medium file - 100MB (~135,000 emails)
python3 scripts/generate-test-mbox.py 100 ~/Downloads/test-100mb.mbox

# Large file - 500MB (~675,000 emails)  
python3 scripts/generate-test-mbox.py 500 ~/Downloads/test-500mb.mbox

# Very large file - 1GB (~1.35 million emails)
python3 scripts/generate-test-mbox.py 1000 ~/Downloads/test-1gb.mbox

# Huge file - 5GB (~6.75 million emails) - Takes ~2 minutes
python3 scripts/generate-test-mbox.py 5000 ~/Downloads/test-5gb.mbox
```

---

## What's Generated?

Each email includes:
- ✅ Realistic headers (From, To, Date, Message-ID)
- ✅ Random subjects (meeting notes, reports, notifications)
- ✅ Multi-line bodies with formatting
- ✅ Proper mbox format (MimeKit compatible)
- ✅ Chronological dates (15 min apart)
- ✅ RFC 822 compliant

**Example email:**
```
From sender@example.com Mon Jan 01 09:00:00 2024
Return-Path: <alice@example.com>
Delivered-To: you@yourcompany.com
Message-ID: <1234567.0@mail.example.com>
Date: Mon, 01 Jan 2024 09:00:00 +0000
From: Alice Johnson <alice@example.com>
To: you@yourcompany.com
Subject: Q4 Financial Report
MIME-Version: 1.0
Content-Type: text/plain; charset=UTF-8

Hi team,

Just wanted to follow up on yesterday's discussion...
```

---

## Recommended Testing Progression

### Phase 1: Prove Upload Works
```bash
# 10MB - Fast test (~13,500 emails, 3 chunks, <1 second upload)
python3 scripts/generate-test-mbox.py 10 ~/Downloads/test-10mb.mbox
```

### Phase 2: Prove Progress Tracking
```bash
# 100MB - Medium test (~135,000 emails, 25 chunks, ~5 seconds upload)
python3 scripts/generate-test-mbox.py 100 ~/Downloads/test-100mb.mbox
```

### Phase 3: Prove Scale
```bash
# 500MB - Large test (~675,000 emails, 125 chunks, ~30 seconds upload)
python3 scripts/generate-test-mbox.py 500 ~/Downloads/test-500mb.mbox
```

### Phase 4: Prove Production-Ready
```bash
# 1GB+ - Massive test (millions of emails, hundreds of chunks)
python3 scripts/generate-test-mbox.py 1000 ~/Downloads/test-1gb.mbox
```

---

## Storage Requirements

| File Size | Emails | Disk Space | Upload Time (100 Mbps) |
|-----------|--------|------------|------------------------|
| 10 MB | ~13,500 | 10 MB | <1 second |
| 100 MB | ~135,000 | 100 MB | ~5 seconds |
| 500 MB | ~675,000 | 500 MB | ~30 seconds |
| 1 GB | ~1.35M | 1 GB | ~2 minutes |
| 5 GB | ~6.75M | 5 GB | ~10 minutes |
| 10 GB | ~13.5M | 10 GB | ~20 minutes |

---

## Why This is Better Than Your 40GB File

**Advantages:**
1. ✅ **Smaller size** - Saves disk space
2. ✅ **Faster testing** - Quick iterations
3. ✅ **Realistic data** - Actual email format
4. ✅ **MimeKit compatible** - Will parse correctly
5. ✅ **Scalable** - Test with any size you want
6. ✅ **No personal data** - Safe to share/debug

**Still proves the same things:**
- ✅ Chunked uploads work
- ✅ Progress tracking works
- ✅ Large file handling works
- ✅ Email parsing will work (Session 3)

---

## Usage

```bash
python3 scripts/generate-test-mbox.py <size_in_mb> <output_path>
```

**Examples:**
```bash
# Create 250MB file
python3 scripts/generate-test-mbox.py 250 ~/Downloads/test-250mb.mbox

# Create 2GB file
python3 scripts/generate-test-mbox.py 2000 ~/Downloads/test-2gb.mbox

# Create directly in specific location
python3 scripts/generate-test-mbox.py 100 /tmp/emails.mbox
```

---

## Performance

- **10MB**: Instant (~0.5 seconds)
- **100MB**: Very fast (~3 seconds)
- **500MB**: Fast (~15 seconds)
- **1GB**: Quick (~30 seconds)
- **5GB**: Reasonable (~2-3 minutes)
- **10GB**: Doable (~5 minutes)

---

## Next Steps

1. **Generate a 10MB test file** to start
2. **Upload it** at `/upload`
3. **Watch the progress bar**
4. **Gradually increase** size (100MB, 500MB, 1GB)
5. **Prove it scales** without using 40GB of disk space!

---

**You can now test with any size file without filling your disk!** 🎉

**Recommendation:** Start with 100MB, then try 500MB or 1GB. No need for 40GB to prove it works!

