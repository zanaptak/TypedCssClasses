const fs = require( 'fs' )

const tailwindConfigFile = fs.realpathSync( process.argv[ 2 ] )
const inputFile = fs.realpathSync( process.argv[ 3 ] )

const postcss = require( 'postcss' )
const tailwindcss = require( 'tailwindcss' )( tailwindConfigFile )

const css = fs.readFileSync( inputFile )

postcss()
  .use( tailwindcss )
  .process( css , { from: inputFile } )
  .then( result => {
    // write included files as leading output lines
    result.messages
      .filter( msg => msg.type === "dependency" && msg.file !== inputFile )
      .forEach( msg => console.log( msg.file ) )
    // write the final CSS output
    console.log( "/* ---- end included files, begin generated CSS ---- */" )
    console.log( result.css )
  } )
  .catch( reason => {
    console.error( reason )
    process.exit( 1 )
  } )
