const path = require('path');

const HtmlWebpackPlugin = require('html-webpack-plugin'); // Inject <script> or <link> tags for generated bundles into index.html template
const CopyWebpackPlugin = require('copy-webpack-plugin'); // Copy static assets to output directory
const MiniCssExtractPlugin = require('mini-css-extract-plugin'); // Extract CSS from bundle to a different file
const ReactRefreshWebpackPlugin = require('@pmmmwh/react-refresh-webpack-plugin'); // Enable React Fast Refresh

// Development mode if running 'webpack serve'
const isDevelopment = process.env.WEBPACK_SERVE === 'true'
console.log('Bundling for ' + (isDevelopment ? 'development' : 'production') + '...');

// Note: Tailwind performs CSS purging based on NODE_ENV environment variable, not webpack mode,
// so production builds should be launched with 'webpack --node-env production'

module.exports = {
    mode: isDevelopment ? 'development' : 'production',

    entry: {
        app: ['./src/Main.fs.js', './content/tailwind-source.css']
    },

    // Add a hash to the output file name in production to prevent browser caching if code changes
    output: {
        filename: isDevelopment ? '[name].js' : '[name].[contenthash].js',
    },

    // Enable source maps in development mode
    ...(isDevelopment && { devtool: 'eval-source-map' }),

    plugins: [
        new HtmlWebpackPlugin({
            filename: 'index.html',
            template: './content/index.html'
        }),
        !isDevelopment && new CopyWebpackPlugin({ patterns: [{ from: './content/assets' }] }),
        !isDevelopment && new MiniCssExtractPlugin({ filename: 'style.[contenthash].css' }),
        isDevelopment && new ReactRefreshWebpackPlugin(),
    ].filter(Boolean),

    devServer: {
        static: {
            directory: path.resolve(__dirname, './content/assets'),
            publicPath: '/',
        }
    },

    module: {
        rules: [
            {
                test: /\.js$/,
                enforce: "pre",
                use: ["source-map-loader"],
            },
            {
                test: /\.js$/,
                exclude: /node_modules/,
                use: {
                    loader: 'babel-loader',
                    options: {
                        presets: [
                            [
                                // Use babel-preset-env to generate JS compatible with most-used browsers.
                                // More info at https://babeljs.io/docs/en/babel-preset-env.html
                                '@babel/preset-env',
                                {
                                    // This adds polyfills when needed. Requires core-js dependency.
                                    // See https://babeljs.io/docs/en/babel-preset-env#usebuiltins
                                    // Note that you still need to add custom polyfills if necessary (e.g. whatwg-fetch)
                                    useBuiltIns: 'usage',
                                    corejs: '3.15',
                                }
                            ],
                            '@babel/preset-react'
                        ],
                        plugins: [
                            isDevelopment && require.resolve('react-refresh/babel'),
                        ].filter(Boolean),
                    }
                }
            },
            {
                test: /\.css$/,
                use: [
                    isDevelopment ? 'style-loader' : MiniCssExtractPlugin.loader,
                    'css-loader',
                    'postcss-loader',
                ].filter(Boolean),
            },
        ]
    }
};
