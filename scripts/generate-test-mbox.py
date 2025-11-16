#!/usr/bin/env python3
"""
Generate realistic .mbox test files with simulated emails.
Usage: python3 generate-test-mbox.py <size_mb> <output_file>
Example: python3 generate-test-mbox.py 100 ~/Downloads/test-100mb.mbox
"""

import sys
import random
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
    "GitHub: New pull request in evermail/evermail",
    "Stripe: Payment successful",
    "Zoom: Meeting reminder",
    "LinkedIn: You appeared in 8 searches this week",
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
{sender}""",
    """Hello,

Please find attached the report you requested. Let me know if you need any clarifications.

Thanks!
{sender}""",
    """Quick update:

- Completed tasks A, B, and C
- Working on task D
- Blocked on task E (waiting for approval)

Will send detailed update by EOD.

{sender}""",
    """Hi,

I've reviewed the document and have a few comments:

1. Section 2.3 needs more detail
2. The timeline in section 4 seems optimistic
3. Budget allocation should be revised

Can we schedule a call to discuss?

Best,
{sender}""",
    """Team,

Reminder: We have our weekly standup tomorrow at 10 AM.

Agenda:
- Sprint review
- Blockers discussion
- Next week planning

See you there!
{sender}""",
]

def generate_email(index, base_date):
    """Generate a single email in mbox format"""
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
    
    # Build email in mbox format
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

def generate_mbox(size_mb, output_file):
    """Generate an mbox file of approximately the specified size"""
    print(f"Generating {size_mb}MB .mbox file...")
    
    target_size = size_mb * 1024 * 1024  # Convert to bytes
    current_size = 0
    email_count = 0
    base_date = datetime(2024, 1, 1, 9, 0, 0)
    
    with open(output_file, 'w', encoding='utf-8') as f:
        while current_size < target_size:
            email = generate_email(email_count, base_date)
            f.write(email)
            current_size += len(email.encode('utf-8'))
            email_count += 1
            
            # Progress update every 100 emails
            if email_count % 100 == 0:
                progress = (current_size / target_size) * 100
                print(f"Progress: {progress:.1f}% ({email_count} emails, {current_size / 1024 / 1024:.1f} MB)")
    
    actual_size_mb = current_size / 1024 / 1024
    print(f"\n✅ Generated {output_file}")
    print(f"   Size: {actual_size_mb:.2f} MB")
    print(f"   Emails: {email_count:,}")
    print(f"   Average email size: {current_size / email_count:.0f} bytes")

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python3 generate-test-mbox.py <size_mb> <output_file>")
        print("Example: python3 generate-test-mbox.py 100 ~/Downloads/test-100mb.mbox")
        sys.exit(1)
    
    try:
        size_mb = int(sys.argv[1])
        output_file = sys.argv[2]
        
        if size_mb < 1:
            print("Error: Size must be at least 1 MB")
            sys.exit(1)
        
        if size_mb > 10000:
            print(f"Warning: Generating {size_mb}MB file will take a while...")
        
        generate_mbox(size_mb, output_file)
        
    except ValueError:
        print("Error: Size must be a number")
        sys.exit(1)
    except Exception as e:
        print(f"Error: {e}")
        sys.exit(1)

