import os
import xml.etree.ElementTree as ET
from openai import OpenAI

# Función para leer el archivo XML y extraer el texto
def extract_texts_from_xml(file_path):
    tree = ET.parse(file_path)
    root = tree.getroot()

    name_texts = []
    description_texts = []

    for monstermon in root.findall('.//Monstermon'):
        name = monstermon.find('name')
        if name is not None:
            name_texts.append(name.text)
        
        action_description = monstermon.find('.//action/description')
        if action_description is not None:
            description_texts.append(action_description.text)

        reaction_description = monstermon.find('.//reaction/description')
        if reaction_description is not None:
            description_texts.append(reaction_description.text)
    
    return tree, root, name_texts, description_texts

# Función para traducir textos usando la API de OpenAI
def translate_texts(texts, client):
    translated_texts = []

    for text in texts:
        context_messages = [{"role": "system", "content": r"Eres un traductor del ingles al español y mantendrás el humor de los mensajes. No puedes añadir frases extras. No traduzcas la palabra Monstermon."}]
        context_messages.append({"role": "user", "content": f"{text}"})
        response = client.chat.completions.create(
            model="gpt-4o-mini",
            messages=context_messages,
            temperature=0.2
        )
        translated_text = response.choices[0].message.content.strip()
        print(f"Original: {text}\nTranslated: {translated_text}\n")
        translated_texts.append(translated_text)

    return translated_texts

# Función para escribir los textos traducidos en un nuevo archivo XML
def write_translated_xml(tree, root, name_texts, description_texts, translated_texts, output_file_path):
    name_index = 0
    description_index = 0

    for monstermon in root.findall('.//Monstermon'):
        name = monstermon.find('name')
        if name is not None:
            name.text = translated_texts[name_index]
            name_index += 1
        
        action_description = monstermon.find('.//action/description')
        if action_description is not None:
            action_description.text = translated_texts[len(name_texts) + description_index]
            description_index += 1

        reaction_description = monstermon.find('.//reaction/description')
        if reaction_description is not None:
            reaction_description.text = translated_texts[len(name_texts) + description_index]
            description_index += 1

    os.makedirs(os.path.dirname(output_file_path), exist_ok=True)
    tree.write(output_file_path, encoding='Windows-1252', xml_declaration=True)

# Configuración del cliente OpenAI
client = OpenAI(api_key="API_KEY")

# Función para procesar todos los archivos txt en la carpeta y subcarpetas
def process_files_in_directory(input_dir, output_dir):
    for subdir, _, files in os.walk(input_dir):
        for file in files:
            if file.endswith('.txt'):
                input_file_path = os.path.join(subdir, file)
                relative_path = os.path.relpath(input_file_path, input_dir)
                output_file_path = os.path.join(output_dir, relative_path)
                
                # Extraer textos del archivo XML
                tree, root, name_texts, description_texts = extract_texts_from_xml(input_file_path)
                
                # Traducir los textos extraídos
                all_texts = name_texts + description_texts
                translated_texts = translate_texts(all_texts, client)
                
                # Escribir los textos traducidos en un nuevo archivo XML
                write_translated_xml(tree, root, name_texts, description_texts, translated_texts, output_file_path)
                
                print(f"Traducción completada y guardada en {output_file_path}")

# Ruta de los directorios de entrada y salida
input_dir = r'C:\input\path'
output_dir = r'C:\output\path'

# Procesar todos los archivos en el directorio
process_files_in_directory(input_dir, output_dir)
