# JSON/XML Tasks

A Critical Manufacturing MES Community **Connect IoT Controller Engine** Task
Library for converting JSON values to XML and XML strings to JSON objects. The
package is published as `@criticalmanufacturing/connect-iot-controller-engine-custom-jsonxml-tasks`
and installed by the `Cmf.Community.IoTJSONXML.IoT` IoT package.

## Tasks

### `Json2xmlTask`

Converts a JSON value into an XML string using `fast-xml-parser`'s
`XMLBuilder`.

| Input | Description |
|---|---|
| `activate` | Starts the conversion. |
| `json` | JSON value to convert. |
| `key` | Optional root element name. When empty, the JSON value is used directly as the XML root. |
| `options` | Optional `fast-xml-parser` builder configuration. Set `useCheerio` to normalize the generated XML with Cheerio. |

| Output | Description |
|---|---|
| `xml` | Generated XML string. |
| `success` | Emitted after the XML is generated. |

### `Xml2jsonTask`

Converts an XML string into a JSON object using `fast-xml-parser`'s
`XMLParser`.

| Input | Description |
|---|---|
| `activate` | Starts the conversion. |
| `xml` | XML string to parse. |
| `options` | Optional `fast-xml-parser` parser configuration. |

| Output | Description |
|---|---|
| `json` | Parsed JSON object. |
| `success` | Emitted after parsing succeeds. |
| `error` | Emitted when parsing fails. |

## Development

Run these commands from this directory:

```bash
npm install
npm run build
npm test
```

`npm run build` compiles both the task library and its TypeScript unit tests.
`npm test` builds the package first and then runs the compiled Mocha tests.

For continuous development, use `npm run watchPackage` for the task library or
`npm run watchTests` for the unit tests.

## Package Structure

- `src/tasks/json2xml/` — JSON-to-XML task implementation.
- `src/tasks/xml2json/` — XML-to-JSON task implementation.
- `test/unit/tasks/` — Mocha and Chai unit tests for both tasks.
- `ui.xml` — Connect IoT designer metadata injected during package deployment.
