var path = require("path");

module.exports = {
    mode: "development",
    entry: "./src/TestWithFable.fsproj",
    output: {
        path: path.join(__dirname, "./public"),
        filename: "bundle.js",
    },
    devServer: {
        static: {
            directory: path.resolve(__dirname, './public'),
        }
    },
    module: {
        rules: [
            {
                test: /\.fs(x|proj)?$/,
                use: "fable-loader"
            }
            , {
                test: /\.css$/i,
                use: ['style-loader', 'css-loader'],
            }
            , {
                test: /\.s[ac]ss$/i,
                use: ['style-loader', 'css-loader', 'sass-loader'],
            }
        ]
    }
}
