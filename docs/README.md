# WEB AXIN - estrategia de dominios y documentacion

Este directorio contiene la documentacion web publicada para los mods AXIN.
En el proyecto real la carpeta se llama `wiki`; cuando se hable de `WEB`, esta
carpeta es el apartado web equivalente.

## Decision de producto

`axin.es` debe reservarse para una futura web principal del ecosistema AXIN.
Esa web principal sera la puerta de entrada general a:

- mods AXIN,
- documentacion,
- servicios web,
- dashboard,
- descargas,
- guias de instalacion,
- estado de servicios.

Por eso los servicios concretos no deberian ocupar el dominio raiz.

## Subdominios recomendados

Usar subdominios para servicios concretos:

- `www.axin.es` o `axin.es`: portal principal futuro del ecosistema.
- `bridgerelay.axin.es`: relay publico de AXINMobileServerBridge.
- `docs.axin.es` o `wiki.axin.es`: documentacion si se separa de GitHub Pages.
- `dashboard.axin.es`: posible entrada futura a dashboards o selector de apps.

La documentacion actual todavia puede publicarse desde GitHub Pages, pero debe
estar escrita pensando en esa separacion futura.

## Regla documental

La guia principal de cada mod debe responder primero a una pregunta humana:

> Que tengo que hacer para instalarlo y usarlo?

Despues, si hace falta, se enlaza a documentacion avanzada.

No mezclar en la primera ruta de lectura:

- contrato interno HTTP,
- detalles de SQLite,
- `ownerToken`,
- headers,
- clases C#,
- hilos internos,
- decisiones historicas de hosting.

## Reparto recomendado

### Guia del mod

Archivo actual:

```text
wiki/axinmobileserverbridge.html
```

Debe contener:

- que hace el mod,
- instalacion recomendada,
- configuracion minima,
- que comando usar,
- que URL debe abrir el usuario,
- errores comunes en lenguaje humano,
- modo avanzado separado.

### Documentacion tecnica del relay

Archivo actual:

```text
10 AXINMobileServerBridge/relay/README.md
```

Debe contener:

- endpoints internos,
- persistencia,
- despliegue tecnico,
- seguridad,
- detalles para mantener el servicio.

### Documentacion historica o de arquitectura

Archivos actuales:

```text
10 AXINMobileServerBridge/docs/
10 AXINMobileServerBridge/00Documentacion/
```

Debe contener:

- ADRs,
- planes,
- decisiones tecnicas,
- pruebas,
- notas de desarrollo.

No debe ser la primera lectura para un admin normal.
