# Mi Primer Addon

<div align="center">

![.NET Framework 4.8](https://img.shields.io/badge/.NET-Framework%204.8-512BD4?logo=dotnet)
![SAP Business One](https://img.shields.io/badge/SAP-Business%20One-0D6EFD)
![C#](https://img.shields.io/badge/C%23-Programming-239120?logo=csharp&logoColor=white)
![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)

</div>

Add-on personalizado para SAP Business One, construido con C# y .NET Framework 4.8, orientado a la integración con la UI API de SAP y a la consulta de tipos de cambio.

## ✨ Vista general

Este proyecto crea un menú dentro de SAP Business One y permite abrir un formulario para consultar valores de cambio de:

- Dólar (USD)
- Euro (EUR)
- UF (Unidad de Fomento)

La lógica está diseñada para ser extensible y servirá como base para futuras funcionalidades del add-on.

## 🚀 Características

- Menú personalizado dentro del entorno de SAP Business One
- Integración con `SAPbouiCOM` y `SAPbobsCOM`
- Formulario de consulta con fecha y resultados
- Validación de entradas y manejo de errores básicos
- Estructura lista para crecer con nuevas pantallas o funcionalidades
- Compatible con .NET Framework 4.8

## 🧩 Requisitos

- Windows
- .NET Framework 4.8
- Visual Studio 2022 o superior
- SAP Business One con soporte de add-ons y UI API

## 🗂️ Estructura del repositorio

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

## 🛠️ Cómo ejecutarlo

1. Clona este repositorio.
2. Abre la solución `MiprimerAddon.slnx` en Visual Studio.
3. Compila el proyecto en `Debug` o `Release`.
4. Registra el add-on según la configuración de tu entorno SAP Business One.
5. Desde el menú de SAP, accede a `Mi Primer Addon` y abre el formulario.

## 📌 Flujo de uso

1. El add-on agrega un menú en SAP Business One.
2. El usuario accede a la opción `Abrir formulario`.
3. Se muestra una pantalla con una fecha y los campos de resultados.
4. Al pulsar `Consultar`, se procesa la información y se visualizan los valores asociados.

## 📈 Estado del proyecto

Proyecto en desarrollo con una base funcional para consultas de tipo de cambio y preparado para futuras ampliaciones.

## 🧭 Roadmap sugerido

- Mejorar validaciones y manejo de errores
- Añadir más monedas o indicadores
- Mejorar la interfaz del formulario
- Modularizar la lógica de negocio
- Preparar la versión para despliegue o entrega interna

## 📄 Licencia

Este proyecto está licenciado bajo la licencia MIT. Consulta el archivo `LICENSE` para más detalles.

## 👤 Autor

Proyecto personal para explorar el desarrollo de add-ons en SAP Business One con C# y .NET Framework.

## 🔗 Repositorio

- GitHub: https://github.com/waifu3/MiPrimerAdd-on
