#!/usr/bin/env python3
"""
Generate realistic .mbox test files with emails that have attachments.
Usage: python3 generate-test-mbox-with-attachments.py <size_mb> <output_file>
Example: python3 generate-test-mbox-with-attachments.py 100 ~/Downloads/test-with-attachments-100mb.mbox
"""

import sys
import random
import base64
from datetime import datetime, timedelta

# Sample data for generating realistic emails
SUBJECTS = [
    "Q4 Financial Report",
    "Team Meeting Notes",
    "Project Status Update",
    "RE: Budget Approval",
    "FW: Client Feedback",
    "Weekly Newsletter",
    "Action Required: Review Document",
    "Invitation: Company All-Hands",
    "Your Amazon.com order has shipped",
    "GitHub: New pull request",
    "Stripe: Payment successful",
    "Zoom: Meeting reminder",
    "Important Security Update",
    "RE: Question about implementation",
    "Holiday Schedule 2024",
    "New features released!",
    "Server maintenance notification",
]

FROM_ADDRESSES = [
    "alice@example.com",
    "bob@company.com",
    "charlie@startup.io",
    "david@enterprise.org",
    "eve@consulting.com",
    "frank@agency.net",
    "grace@university.edu",
    "henry@government.gov",
]

FROM_NAMES = [
    "Alice Johnson",
    "Bob Smith",
    "Charlie Davis",
    "David Wilson",
    "Eve Martinez",
    "Frank Anderson",
    "Grace Lee",
    "Henry Taylor",
]

TO_ADDRESSES = [
    "you@yourcompany.com",
    "team@company.com",
    "engineering@startup.io",
]

BODY_TEMPLATES = [
    """Hi team,

Just wanted to follow up on yesterday's discussion. I think we should move forward with the proposed changes.

Let me know your thoughts.

Best regards,
{sender}
""",
    """Hello,

Please find attached the document we discussed.

Thanks,
{sender}
""",
    """Hi there,

I've attached the files you requested. Let me know if you need anything else.

Best,
{sender}
""",
]

# Attachment types and sizes (in bytes)
ATTACHMENT_TYPES = [
    ("application/pdf", "document.pdf", 50000),  # 50KB PDF
    ("image/jpeg", "photo.jpg", 200000),  # 200KB JPEG
    ("application/vnd.ms-excel", "spreadsheet.xlsx", 75000),  # 75KB Excel
    ("application/msword", "document.doc", 60000),  # 60KB Word
    ("text/csv", "data.csv", 30000),  # 30KB CSV
    ("application/zip", "archive.zip", 150000),  # 150KB ZIP
    ("image/png", "screenshot.png", 180000),  # 180KB PNG
    ("application/json", "data.json", 25000),  # 25KB JSON
]

def generate_random_data(size_bytes):
    """Generate random base64-encoded data for attachments"""
    # Generate random bytes
    random_bytes = bytes(random.randint(0, 255) for _ in range(size_bytes))
    # Encode as base64
    return base64.b64encode(random_bytes).decode('ascii')

def generate_email(index, base_date, include_attachment=False):
    """Generate a single email in mbox format, optionally with attachment"""
    sender_name = random.choice(FROM_NAMES)
    sender_email = random.choice(FROM_ADDRESSES)
    to_email = random.choice(TO_ADDRESSES)
    subject = random.choice(SUBJECTS)
    body = random.choice(BODY_TEMPLATES).format(sender=sender_name)
    
    # Date incrementing for each email
    email_date = base_date + timedelta(minutes=index * 15)
    date_str = email_date.strftime("%a, %d %b %Y %H:%M:%S +0000")
    
    # Message ID
    message_id = f"<{random.randint(1000000, 9999999)}.{index}@mail.example.com>"
    
    # Build email
    if include_attachment:
        # Choose random attachment
        content_type, filename, size_bytes = random.choice(ATTACHMENT_TYPES)
        attachment_data = generate_random_data(size_bytes)
        boundary = f"----=_Part_{index}_{random.randint(1000, 9999)}"
        
        email = f"""From sender@example.com {email_date.strftime("%a %b %d %H:%M:%S %Y")}
Return-Path: <{sender_email}>
Delivered-To: {to_email}
Received: from mail.example.com (mail.example.com [192.168.1.1])
    by mx.example.com (Postfix) with ESMTP id ABC123
    for <{to_email}>; {date_str}
Message-ID: {message_id}
Date: {date_str}
From: {sender_name} <{sender_email}>
To: {to_email}
Subject: {subject}
MIME-Version: 1.0
Content-Type: multipart/mixed; boundary="{boundary}"

This is a multi-part message in MIME format.

--{boundary}
Content-Type: text/plain; charset=UTF-8
Content-Transfer-Encoding: 7bit

{body}

--{boundary}
Content-Type: {content_type}
Content-Transfer-Encoding: base64
Content-Disposition: attachment; filename="{filename}"

{attachment_data}

--{boundary}--

"""
    else:
        email = f"""From sender@example.com {email_date.strftime("%a %b %d %H:%M:%S %Y")}
Return-Path: <{sender_email}>
Delivered-To: {to_email}
Received: from mail.example.com (mail.example.com [192.168.1.1])
    by mx.example.com (Postfix) with ESMTP id ABC123
    for <{to_email}>; {date_str}
Message-ID: {message_id}
Date: {date_str}
From: {sender_name} <{sender_email}>
To: {to_email}
Subject: {subject}
MIME-Version: 1.0
Content-Type: text/plain; charset=UTF-8
Content-Transfer-Encoding: 7bit
X-Mailer: Example Mail Client 1.0

{body}

"""
    return email

def generate_mbox(size_mb, output_file, attachment_percentage=30):
    """Generate an mbox file with emails, some with attachments"""
    print(f"Generating {size_mb}MB .mbox file with attachments...")
    print(f"  - {attachment_percentage}% of emails will have attachments")
    
    target_size = size_mb * 1024 * 1024  # Convert to bytes
    current_size = 0
    email_count = 0
    emails_with_attachments = 0
    base_date = datetime(2024, 1, 1, 9, 0, 0)
    
    with open(output_file, 'w', encoding='utf-8') as f:
        while current_size < target_size:
            # 30% of emails have attachments
            has_attachment = random.randint(1, 100) <= attachment_percentage
            
            email = generate_email(email_count, base_date, include_attachment=has_attachment)
            f.write(email)
            current_size += len(email.encode('utf-8'))
            email_count += 1
            
            if has_attachment:
                emails_with_attachments += 1
            
            # Progress update every 100 emails
            if email_count % 100 == 0:
                progress = (current_size / target_size) * 100
                print(f"Progress: {progress:.1f}% ({email_count} emails, {emails_with_attachments} with attachments, {current_size / 1024 / 1024:.1f} MB)")
    
    actual_size_mb = current_size / 1024 / 1024
    print(f"\n✅ Generated {output_file}")
    print(f"   Size: {actual_size_mb:.2f} MB")
    print(f"   Total emails: {email_count:,}")
    print(f"   Emails with attachments: {emails_with_attachments:,} ({emails_with_attachments/email_count*100:.1f}%)")
    print(f"   Average email size: {current_size / email_count:.0f} bytes")

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python3 generate-test-mbox-with-attachments.py <size_mb> <output_file>")
        print("Example: python3 generate-test-mbox-with-attachments.py 100 ~/Downloads/test-with-attachments-100mb.mbox")
        sys.exit(1)
    
    try:
        size_mb = int(sys.argv[1])
        output_file = sys.argv[2]
        
        if size_mb <= 0:
            print("Error: Size must be greater than 0")
            sys.exit(1)
        
        generate_mbox(size_mb, output_file)
    except ValueError:
        print("Error: Size must be a number")
        sys.exit(1)
    except Exception as e:
        print(f"Error: {e}")
        sys.exit(1)

