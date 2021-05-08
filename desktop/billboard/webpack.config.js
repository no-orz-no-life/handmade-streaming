const path = require("path")
const webpack = require("webpack")
module.exports = {
    mode: "development",
    entry: "./src/index.js",
    output: {
        filename: "index.js",
        path: path.join(__dirname, "public")
    },
    module: {
        rules: [
            {
                test: /\.js$/,
                exclude: /node_modules/,
                use: [
                    {
                        loader: "babel-loader",
                        options: {
                            presets: ["@babel/preset-env"]
                        }
                    }
                ],
            },
            {
                test: /\.css$/,
                use: ["style-loader", "css-loader"],
            },
            {
                // 拡張子がsassとscssのファイルを対象とする
                test: /\.s[ac]ss$/i,
                use: [
                  "style-loader",
                  "css-loader",
                  "sass-loader",
                ],
            },
            {
                test: /\.(gif|png|jpg|eot|wof|woff|ttf|svg)$/,
                type: "asset/inline",
            },
        ]
    },
    resolve: {
        extensions: [".js"],
        modules: ["node_modules"],
    },
}