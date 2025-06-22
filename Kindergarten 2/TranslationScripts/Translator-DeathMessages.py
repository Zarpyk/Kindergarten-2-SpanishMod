import os
import xml.etree.ElementTree as ET
from openai import OpenAI

# Función para leer el archivo XML y extraer el texto
def extract_texts_from_xml(file_path):
    tree = ET.parse(file_path)
    root = tree.getroot()

    panel_texts = []
    message_texts = []

    panel_elements = [
        'TitleText', 'LilyText', 'BillyText', 'RestartCurrentText', 
        'RestartOtherText', 'RestartFromBedroomText', 'QuitText'
    ]
    for element in panel_elements:
        elem = root.find(f'.//{element}')
        if elem is not None:
            panel_texts.append(elem.text)
    
    for death_message in root.findall('.//DeathMessage'):
        message = death_message.find('Message')
        if message is not None:
            message_texts.append(message.text)
    
    return tree, root, panel_texts, message_texts

# Función para traducir textos usando la API de OpenAI
def translate_texts(texts, client):
    translated_texts = []
    context_messages = [{"role": "system", "content": r"Eres un traductor del inglés al español y mantendrás el humor de los mensajes. No puedes añadir frases extras."}]

    for text in texts:
        context_messages.append({"role": "user", "content": f"{text}"})
        response = client.chat.completions.create(
            model="gpt-4o-mini",
            messages=context_messages,
            temperature=0.5
        )
        translated_text = response.choices[0].message.content.strip()
        print(f"Original: {text}\nTranslated: {translated_text}\n")
        translated_texts.append(translated_text)
        context_messages.append({"role": "assistant", "content": translated_text})

    return translated_texts

# Función para escribir los textos traducidos en un nuevo archivo XML
def write_translated_xml(tree, root, panel_texts, message_texts, translated_texts, output_file_path):
    panel_index = 0
    message_index = 0

    panel_elements = [
        'TitleText', 'LilyText', 'BillyText', 'RestartCurrentText', 
        'RestartOtherText', 'RestartFromBedroomText', 'QuitText'
    ]
    for element in panel_elements:
        elem = root.find(f'.//{element}')
        if elem is not None:
            elem.text = translated_texts[panel_index]
            panel_index += 1
    
    for death_message in root.findall('.//DeathMessage'):
        message = death_message.find('Message')
        if message is not None:
            message.text = translated_texts[len(panel_texts) + message_index]
            message_index += 1

    os.makedirs(os.path.dirname(output_file_path), exist_ok=True)
    tree.write(output_file_path, encoding='Windows-1252', xml_declaration=True)

# Configuración del cliente OpenAI
client = OpenAI(api_key="API_KEY")

# Función para procesar todos los archivos xml en la carpeta y subcarpetas
def process_files_in_directory(input_dir, output_dir):
    for subdir, _, files in os.walk(input_dir):
        for file in files:
            if file.endswith('.txt'):
                input_file_path = os.path.join(subdir, file)
                relative_path = os.path.relpath(input_file_path, input_dir)
                output_file_path = os.path.join(output_dir, relative_path)
                
                # Extraer textos del archivo XML
                tree, root, panel_texts, message_texts = extract_texts_from_xml(input_file_path)
                
                # Traducir los textos extraídos
                all_texts = panel_texts + message_texts
                translated_texts = translate_texts(all_texts, client)
                
                # Escribir los textos traducidos en un nuevo archivo XML
                write_translated_xml(tree, root, panel_texts, message_texts, translated_texts, output_file_path)
                
                print(f"Traducción completada y guardada en {output_file_path}")

# Ruta de los directorios de entrada y salida
input_dir = r'C:\input\path'
output_dir = r'C:\output\path'

# Procesar todos los archivos en el directorio
process_files_in_directory(input_dir, output_dir)
