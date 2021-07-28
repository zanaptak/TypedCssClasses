const isDevelopment = process.env.WEBPACK_SERVE === 'true';

const cssnano = require('cssnano')({
    preset: ['default', {
        discardComments: { removeAll: true }
    }]
})

module.exports = {
    plugins: [
        require('tailwindcss')
        , require('autoprefixer')
        , !isDevelopment && cssnano
    ].filter(Boolean)
};
