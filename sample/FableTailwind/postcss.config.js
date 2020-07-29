const isProduction = process.env.NODE_ENV === 'production'

const cssnano = require('cssnano')({
  preset: ['default', {
    discardComments: { removeAll: true }
  }]
})

module.exports = {
  plugins: isProduction ? [
    require('tailwindcss')
    , require('autoprefixer')
    , cssnano
  ] : [
    require('tailwindcss')
    , require('autoprefixer')
  ]
}
