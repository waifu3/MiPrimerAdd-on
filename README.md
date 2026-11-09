# Mi Primer Addon

<div align="center">

![Status](https://img.shields.io/badge/status-active-success)
![.NET Framework 4.8](https://img.shields.io/badge/.NET-Framework%204.8-512BD4?logo=dotnet)
![SAP Business One](https://img.shields.io/badge/SAP-Business%20One-0D6EFD)
![C#](https://img.shields.io/badge/C%23-Development-239120?logo=csharp&logoColor=white)
![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)

</div>

Add-on personalizado para SAP Business One, desarrollado con C# y .NET Framework 4.8, pensado para integrar un menú con una pantalla de consulta de tipos de cambio.

## Overview

Este proyecto permite crear un menú dentro de SAP Business One y abrir un formulario que consulta valores de cambio de:

- Dólar (USD)
- Euro (EUR)
- UF (Unidad de Fomento)

La solución está construida sobre la UI API de SAP y está preparada para evolucionar con nuevas funcionalidades.

## Features

- Menú personalizado en SAP Business One
- Formulario de consulta con validación de fecha
- Integración con `SAPbouiCOM` y `SAPbobsCOM`
- Visualización de resultados por moneda
- Base extensible para futuras pantallas y lógicas
- Compatible con .NET Framework 4.8

## Tech Stack

- C#
- .NET Framework 4.8
- SAP Business One UI API
- Visual Studio

## Requirements

- Windows
- .NET Framework 4.8
- Visual Studio 2022 o superior
- SAP Business One con soporte de add-ons / UI API

## Project Structure

```text
MiprimerAddon/
├── .gitignore
├── LICENSE
├── README.md
├── MiprimerAddon.slnx
├── MiprimerAddon/
│   ├── Addon.cs
│   ├── App.config
│   ├── Form1.cs
│   ├── Form1.Designer.cs
│   ├── Form1.resx
│   ├── MiprimerAddon.csproj
│   ├── Program.cs
│   └── Properties/
└──
```

## Getting Started

1. Clona el repositorio.
2. Abre la solución `MiprimerAddon.slnx` en Visual Studio.
3. Compila el proyecto en `Debug` o `Release`.
4. Registra el add-on según la configuración de tu entorno SAP Business One.
5. Desde el menú de SAP, selecciona `Mi Primer Addon` y abre el formulario.

## Usage Flow

1. El add-on registra un menú en SAP Business One.
2. El usuario accede a la opción `Abrir formulario`.
3. La pantalla solicita una fecha y presenta los valores consultados.
4. Al pulsar `Consultar`, se procesan los datos y se muestran los resultados.

## Project Status

Este proyecto se encuentra en desarrollo activo y cuenta con una base funcional lista para ser ampliada.

## Roadmap

- Mejorar validaciones y manejo de errores
- Añadir soporte para más monedas o indicadores
- Mejorar la experiencia visual del formulario
- Modularizar la lógica para mantener el código más limpio
- Preparar una versión estable para entrega o pruebas internas

## License

Este proyecto se distribuye bajo la licencia MIT. Consulta el archivo `LICENSE` para más detalles.

## Author

Proyecto personal para aprender y experimentar con add-ons para SAP Business One usando C# y .NET Framework.

## Repository

- GitHub: https://github.com/waifu3/MiPrimerAdd-on

## Screenshot

> Aquí puedes agregar una captura del formulario o del menú del add-on para mejorar la presentación del repositorio.
