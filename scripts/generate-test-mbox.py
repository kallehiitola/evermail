#!/usr/bin/env python3
"""
Generate realistic .mbox test files with simulated emails.
Usage: python3 generate-test-mbox.py <size_mb> <output_file>
Example: python3 generate-test-mbox.py 100 ~/Downloads/test-100mb.mbox
"""

import sys
import random
import uuid
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

SENDER_POOL = list(zip(FROM_NAMES, FROM_ADDRESSES))
BASE_SUBJECTS = [
    subject.replace("RE: ", "").replace("FW: ", "").replace("Fwd: ", "")
    for subject in SUBJECTS
]

THREAD_START_CHANCE = 0.25
THREAD_CONTINUE_CHANCE = 0.4
MAX_ACTIVE_THREADS = 12

REPLY_INTROS = [
    "Thanks for the quick response.",
    "Adding a few notes inline.",
    "Circling back on this.",
    "Appreciate the context. See my answers below.",
]

FORWARD_INTROS = [
    "Forwarding for visibility.",
    "Sharing this thread with you.",
    "FYI - see conversation below.",
]


class ThreadContext:
    """Track metadata for a simulated email thread."""

    def __init__(self, subject, participants):
        self.subject = subject
        self.participants = participants  # list of (name, email)
        self.message_ids = []
        self.last_message = None
        self.required_followups = random.randint(1, 3)

    def add_message(self, message_id, sender_name, sender_email, date_str, body_preview):
        self.message_ids.append(message_id)
        self.last_message = {
            "message_id": message_id,
            "sender_name": sender_name,
            "sender_email": sender_email,
            "date": date_str,
            "body_preview": body_preview,
        }

    def mark_followup_sent(self):
        if self.required_followups > 0:
            self.required_followups -= 1

    def references_header(self):
        return " ".join(self.message_ids)


def create_thread_context():
    """Create a new thread with random participants and base subject."""
    subject = random.choice(BASE_SUBJECTS)
    participant_count = random.randint(2, min(4, len(SENDER_POOL)))
    participants = random.sample(SENDER_POOL, participant_count)
    return ThreadContext(subject, participants)


def build_body_preview(body):
    """Store just a short preview for quoting to keep memory usage low."""
    trimmed_lines = [line.strip() for line in body.strip().splitlines()[:5]]
    preview = "\n".join(line for line in trimmed_lines if line)
    return preview or "(no additional content)"


def pick_thread_sender(thread_context, previous_sender_email):
    """Pick a sender from thread participants, avoiding repeats when possible."""
    candidates = [p for p in thread_context.participants if p[1] != previous_sender_email]
    if not candidates:
        candidates = thread_context.participants
    return random.choice(candidates)


def pick_thread_recipient(thread_context, sender_email):
    """Pick a recipient in the thread, falling back to default pool if needed."""
    candidates = [email for _, email in thread_context.participants if email != sender_email]
    if not candidates:
        candidates = TO_ADDRESSES
    return random.choice(candidates)


def format_reply_body(sender_name, previous_message):
    quoted_lines = "\n".join(f"> {line}" for line in previous_message["body_preview"].splitlines())
    intro = random.choice(REPLY_INTROS)
    return (
        f"{intro}\n\n"
        f"On {previous_message['date']}, {previous_message['sender_name']} wrote:\n"
        f"{quoted_lines}\n\n"
        f"Thanks,\n{sender_name}"
    )


def format_forward_body(previous_message, to_email, thread_subject, sender_name):
    intro = random.choice(FORWARD_INTROS)
    forwarded_block = (
        "--- Forwarded message ---\n"
        f"From: {previous_message['sender_name']} <{previous_message['sender_email']}>\n"
        f"Date: {previous_message['date']}\n"
        f"Subject: {thread_subject}\n"
        f"To: {to_email}\n\n"
        f"{previous_message['body_preview']}\n"
        "--- End forwarded message ---"
    )
    return f"{intro}\n\n{forwarded_block}\n\nThanks,\n{sender_name}"

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

def generate_email(index, base_date, thread_context=None, thread_action="single"):
    """Generate a single email (optionally as part of a thread) in mbox format."""
    email_date = base_date + timedelta(minutes=index * 15)
    date_str = email_date.strftime("%a, %d %b %Y %H:%M:%S +0000")
    in_reply_to = ""
    references = ""

    if thread_context:
        previous_message = thread_context.last_message
        previous_sender = previous_message["sender_email"] if previous_message else None
        sender_name, sender_email = pick_thread_sender(thread_context, previous_sender)
        to_email = pick_thread_recipient(thread_context, sender_email)
        base_subject = thread_context.subject

        if thread_action == "start" or not previous_message:
            subject = base_subject
            body = random.choice(BODY_TEMPLATES).format(sender=sender_name)
        elif thread_action == "forward":
            subject = f"Fwd: {base_subject}"
            in_reply_to = previous_message["message_id"]
            references = thread_context.references_header()
            body = format_forward_body(previous_message, to_email, base_subject, sender_name)
        else:
            subject = f"Re: {base_subject}"
            in_reply_to = previous_message["message_id"]
            references = thread_context.references_header()
            body = format_reply_body(sender_name, previous_message)
    else:
        sender_name = random.choice(FROM_NAMES)
        sender_email = random.choice(FROM_ADDRESSES)
        to_email = random.choice(TO_ADDRESSES)
        subject = random.choice(SUBJECTS)
        body = random.choice(BODY_TEMPLATES).format(sender=sender_name)

    message_id = f"<{uuid.uuid4()}@mail.example.com>"

    header_lines = [
        f"From sender@example.com {email_date.strftime('%a %b %d %H:%M:%S %Y')}",
        f"Return-Path: <{sender_email}>",
        f"Delivered-To: {to_email}",
        "Received: from mail.example.com (mail.example.com [192.168.1.1])",
        "    by mx.example.com (Postfix) with ESMTP id ABC123",
        f"    for <{to_email}>; {date_str}",
        f"Message-ID: {message_id}",
        f"Date: {date_str}",
        f"From: {sender_name} <{sender_email}>",
        f"To: {to_email}",
        f"Subject: {subject}",
    ]

    if in_reply_to:
        header_lines.append(f"In-Reply-To: {in_reply_to}")
    if references:
        header_lines.append(f"References: {references}")

    header_lines.extend(
        [
            "MIME-Version: 1.0",
            "Content-Type: text/plain; charset=UTF-8",
            "Content-Transfer-Encoding: 7bit",
            "X-Mailer: Example Mail Client 1.0",
            "",
            body,
            "",
        ]
    )

    email = "\n".join(header_lines) + "\n"
    body_preview = build_body_preview(body)
    return email, message_id, sender_name, sender_email, date_str, body_preview

def generate_mbox(size_mb, output_file):
    """Generate an mbox file of approximately the specified size"""
    print(f"Generating {size_mb}MB .mbox file...")
    
    target_size = size_mb * 1024 * 1024  # Convert to bytes
    current_size = 0
    email_count = 0
    base_date = datetime(2024, 1, 1, 9, 0, 0)
    threads = []
    threaded_email_count = 0
    thread_starts = 0
    
    with open(output_file, 'w', encoding='utf-8') as f:
        while current_size < target_size:
            thread_context = None
            thread_action = "single"

            eligible_threads = [t for t in threads if t.last_message]
            forced_followups = [t for t in eligible_threads if t.required_followups > 0]

            if forced_followups:
                thread_context = random.choice(forced_followups)
                thread_action = random.choices(["reply", "forward"], weights=[0.8, 0.2])[0]
            elif eligible_threads and random.random() < THREAD_CONTINUE_CHANCE:
                thread_context = random.choice(eligible_threads)
                thread_action = random.choices(["reply", "forward"], weights=[0.75, 0.25])[0]
            elif len(threads) < MAX_ACTIVE_THREADS and random.random() < THREAD_START_CHANCE:
                thread_context = create_thread_context()
                threads.append(thread_context)
                thread_action = "start"
                thread_starts += 1

            email, message_id, sender_name, sender_email, date_str, body_preview = generate_email(
                email_count,
                base_date,
                thread_context=thread_context,
                thread_action=thread_action,
            )

            f.write(email)
            email_bytes = len(email.encode('utf-8'))
            current_size += email_bytes
            email_count += 1

            if thread_context:
                thread_context.add_message(message_id, sender_name, sender_email, date_str, body_preview)
                if thread_action != "start":
                    thread_context.mark_followup_sent()
                threaded_email_count += 1

            # Progress update every 100 emails
            if email_count % 100 == 0:
                progress = (current_size / target_size) * 100
                print(
                    f"Progress: {progress:.1f}% "
                    f"({email_count} emails, threads: {threaded_email_count}, "
                    f"{current_size / 1024 / 1024:.1f} MB)"
                )
    
    actual_size_mb = current_size / 1024 / 1024
    print(f"\n✅ Generated {output_file}")
    print(f"   Size: {actual_size_mb:.2f} MB")
    print(f"   Emails: {email_count:,}")
    print(f"   Threaded emails: {threaded_email_count:,}")
    print(f"   Threads started: {thread_starts:,}")
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

