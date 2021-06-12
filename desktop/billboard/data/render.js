const asciidoctor = require("asciidoctor")()
const fs = require('fs')
const content = fs.readFileSync("carousel.adoc")
const html = asciidoctor.convert(content)
console.log(html)

