
# TechHost.Service.Template

This repository serves as a template project and forms a base for building any solution.

## Table of Contents
- [Introduction](#introduction)
- [Features](#features)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Installation](#installation)
- [Usage](#usage)
- [Dependencies](#dependencies)
- [Compatibility](#compatibility)
- [Contributing](#contributing)
- [License](#license)
- [Contact](#contact)

## Introduction
TechHost.Service.Template is designed to provide a solid foundation for new projects. It includes a set of essential configurations and tools to streamline the development process.

## Features
- Modular and scalable architecture
- Pre-configured setup for common development tools
- Comprehensive documentation and best practices

## Getting Started

### Prerequisites
Before you begin, ensure you have the following installed on your local machine:
- [.NET SDK](https://dotnet.microsoft.com/download) (version 6.0 or later)
- [Docker](https://www.docker.com/get-started)

### Installation
1. **Clone the repository:**
   ```bash
   git clone https://github.com/UgoOluwa/TechHost.Service.Template.git
   ```
2. **Navigate to the project directory:**
   ```bash
   cd TechHost.Service.Template
   ```
3. **Build the project:**
   ```bash
   dotnet build
   ```

## Usage
To run the application locally, use:
```bash
dotnet run
```
This will launch the project on `http://localhost:5000`.

### Packing to Docker Image
To pack the application into a Docker image, follow these steps:
1. **Build the Docker image:**
   ```bash
   docker build -t techhost-service-template .
   ```
2. **Run the Docker container:**
   ```bash
   docker run -p 5000:80 techhost-service-template
   ```

## Dependencies
This project relies on the following major dependencies:
- `.NET 6.0`
- `ASP.NET Core`

For a complete list of dependencies, refer to the `.csproj` file.

## Compatibility
This project is compatible with the following environments:
- Operating Systems: Windows, macOS, Linux
- Docker: Version 20.x or later

## Contributing
We welcome contributions! Please read our Contributing Guide to learn how you can help.

## License
This project is licensed under the MIT License. See the LICENSE file for details.

## Contact
For any inquiries or issues, please contact [Ugo Oluwa](mailto:ugoluwa@gmail.com).
```
