# Mi Primer Addon

<div align="center">

![.NET Framework 4.8](https://img.shields.io/badge/.NET-Framework%204.8-512BD4?logo=dotnet)
![SAP Business One](https://img.shields.io/badge/SAP-Business%20One-0D6EFD)
![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)

</div>

Add-on para SAP Business One desarrollado en .NET Framework 4.8, pensado para integrar un menú personalizado y una pequeña pantalla de consulta de tipos de cambio.

## Descripción

Este proyecto crea un menú dentro de SAP Business One y permite abrir un formulario desde el menú principal para consultar valores de cambio de:

- Dólar (USD)
- Euro (EUR)
- UF

La interfaz se integra con la UI API de SAP Business One y permite seleccionar una fecha y visualizar los valores asociados.

## Características

- Menú personalizado dentro de SAP Business One
- Formularios integrados con UI API
- Validación de fecha
- Consulta de tipos de cambio por moneda
- Diseño sencillo y extensible para futuras mejoras
- Compatible con .NET Framework 4.8

## Requisitos

- Windows
- .NET Framework 4.8
- Visual Studio 2022 o superior
- SAP Business One con compatibilidad de add-ons / UI API

## Estructura del proyecto

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

## Instalación y uso

1. Clona este repositorio.
2. Abre la solución `MiprimerAddon.slnx` en Visual Studio.
3. Compila el proyecto en `Debug` o `Release`.
4. Registra el add-on según la configuración de tu entorno SAP Business One.
5. Desde el menú de SAP, accede a `Mi Primer Addon` y abre el formulario.

## Ejemplo de flujo

1. El add-on registra un menú dentro de SAP Business One.
2. El usuario selecciona la opción "Abrir formulario".
3. Se abre una pantalla con una fecha y los campos de resultado.
4. Al presionar "Consultar", se ejecuta la lógica de consulta y se muestran los valores correspondientes.

## Estado del proyecto

Este proyecto está en desarrollo activo y preparado para ampliar funcionalidad en futuras versiones.

## Roadmap sugerido

- Mejorar validaciones de entrada
- Añadir soporte para más monedas
- Mejorar la presentación del formulario
- Añadir manejo de errores más robusto
- Preparar el repositorio para despliegue o versión estable

## Licencia

Este proyecto se distribuye bajo la licencia MIT. Consulta el archivo `LICENSE` para más detalles.

## Autor

Proyecto personal desarrollado para aprender y experimentar con add-ons para SAP Business One.
