import csv
import time
import threading
from openai import OpenAI
from concurrent.futures import ThreadPoolExecutor, as_completed
import tiktoken

client = OpenAI(api_key="open-ai-api-key")

SYSTEM_PROMPT = (
    "You are an English to Spanish translator tasked with translating location names and their descriptions.\n\n"
    "# Instructions:\n"
    "- Translate the LOCATION NAME (first line) creatively if necessary, preserving tone.\n"
    "- Translate the DESCRIPTION (second line), maintaining style, mood, or any humor.\n"
    "- Maintain punctuation and quotation marks.\n"
    "- If the phrase includes names or concepts without translation, keep them in English or add a clarification in parentheses if needed.\n\n"
    "# Output Format:\n"
    "[Entry ID: X]\nTranslated Name\nTranslated Description"
)

MAX_CHUNK_SIZE = 30
MAX_WORKERS = 2
TOKENS_PER_MINUTE_LIMIT = 25000
TOKENS_USED = 0
WINDOW_START = time.time()
LOCK = threading.Lock()

def estimate_tokens(text, model="gpt-4"):
    encoding = tiktoken.encoding_for_model(model)
    return len(encoding.encode(text))

def chunk_list(lst, chunk_size):
    for i in range(0, len(lst), chunk_size):
        yield lst[i:i + chunk_size]

def translate_chunk(chunk, idx, total_chunks):
    global TOKENS_USED, WINDOW_START

    prompt = "\n\n".join(f"[Entry ID: {i}]\n{name}\n{desc}" for i, name, desc in chunk)
    estimated_tokens = estimate_tokens(SYSTEM_PROMPT + prompt)

    with LOCK:
        now = time.time()
        elapsed = now - WINDOW_START
        if elapsed > 60:
            TOKENS_USED = 0
            WINDOW_START = now
        elif TOKENS_USED + estimated_tokens > TOKENS_PER_MINUTE_LIMIT:
            wait_time = 60 - elapsed
            print(f"[Chunk {idx}] Waiting {wait_time:.2f}s due to rate limit...")
            time.sleep(wait_time)
            TOKENS_USED = 0
            WINDOW_START = time.time()
        TOKENS_USED += estimated_tokens

    print(f"Translating chunk {idx}/{total_chunks} with {len(chunk)} entries...")
    response = client.chat.completions.create(
        model="gpt-4.1-2025-04-14",
        messages=[
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": prompt}
        ],
        temperature=0.7
    )
    return response.choices[0].message.content.strip()

def parse_translated_block(block):
    entries = {}
    current_id = None
    lines = block.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i].strip()
        if line.startswith("[Entry ID:"):
            current_id = int(line.split(":")[1].strip(" ]"))
            if i + 2 < len(lines):
                name = lines[i + 1].strip().strip('"')
                desc = lines[i + 2].strip().strip('"')
                entries[current_id] = (name, desc)
                i += 3
            else:
                i += 1
        else:
            i += 1
    return entries

def process_locations_csv(input_path, output_path):
    with open(input_path, newline='', encoding='utf-8-sig') as infile:
        reader = list(csv.DictReader(infile))
        entries = [(i, row["Field"], row["Default"]) for i, row in enumerate(reader)]

    chunks = list(chunk_list(entries, MAX_CHUNK_SIZE))
    translations = {}

    with ThreadPoolExecutor(max_workers=MAX_WORKERS) as executor:
        futures = {
            executor.submit(translate_chunk, chunk, idx + 1, len(chunks)): idx
            for idx, chunk in enumerate(chunks)
        }

        for fut in as_completed(futures):
            idx = futures[fut]
            try:
                block = fut.result()
                translations.update(parse_translated_block(block))
            except Exception as e:
                print(f"Error in chunk {idx}: {e}")

    for i, row in enumerate(reader):
        if i in translations:
            translated_name, translated_desc = translations[i]
            row["Nombre traducido [es]"] = translated_name
            row["Ubicación traducida [es]"] = translated_desc
        else:
            row["Nombre traducido [es]"] = row["Field"]
            row["Ubicación traducida [es]"] = row["Default"]

    fieldnames = reader[0].keys()

    with open(output_path, 'w', newline='', encoding='utf-8-sig') as outfile:
        writer = csv.DictWriter(outfile, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(reader)

    print(f"Translation complete. Output saved to: {output_path}")

# Set your input/output file paths
input_csv_path = r'C:\Development\Projects\Kindergarten-2-SpanishMod\Kindergtarten 3\TranslationScripts\MonstermonLocations.csv'
output_csv_path = r'C:\Development\Projects\Kindergarten-2-SpanishMod\Kindergtarten 3\TranslationScripts\MonstermonLocations_es_new.csv'

process_locations_csv(input_csv_path, output_csv_path)
