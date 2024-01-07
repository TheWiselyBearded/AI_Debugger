# AI Unity Debugging Tool with GPT Assistants

## Description
This Unity-based debugging tool uniquely integrates with GPT Assistants, creating a dedicated assistant for each project. It allows users to upload specific files and scans codebases, leveraging the Reflection library. This combination enables the tool to utilize runtime values and pre-uploaded content to assist in debugging and elucidating the codebase at runtime. The tool is designed for both VR and non-VR Unity projects, providing advanced analysis and insights into the relationships among components, their runtime behaviors, and overall code functionality.


## Features
- Proximity-based scanning of VR objects.
- Reflection to gather detailed information about object classes.
- Integration with GPT for advanced analysis of runtime environment.
- Continuous updates and improvements.

## Usage
1. **API Key Configuration**: Initially, create an OpenAI configuration file and insert your API key.
2. **Scan Directories**: Navigate to `Tools > Generate PDF` in Unity to scan directories. This step collects information about your project's codebase.
3. **Create GPT Assistant**: Access the 'Assistants' window by going to `Tools > GPT Assistant Builder`. Here, create your GPT assistant by specifying its name and model.
4. **Attach Files**: In the 'Assistants' window, attach files that include data about your codebase, such as class structures and method descriptions.
5. **Runtime Query**: The tool queries the GPT assistant during runtime, combining the GPT responses with the scanned documentation to provide insights about your code's functionality and assist in troubleshooting.

Note: Proper setup of the Unity project is crucial for the effective use of this debugging tool.

