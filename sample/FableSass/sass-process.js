const sass = require('sass')
const fs = require('fs')
const inputFile = fs.realpathSync(process.argv[2])

// run the preprocessor
const result = sass.renderSync({ file: inputFile })

// write included files as leading output lines
result.stats.includedFiles
    .filter(f => f !== inputFile)
    .forEach(f => console.log(f))

// write the final CSS output
console.log("/* ---- end included files, begin generated CSS ---- */")
console.log(result.css.toString())
