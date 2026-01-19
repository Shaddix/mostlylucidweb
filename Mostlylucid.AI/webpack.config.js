const TerserPlugin = require('terser-webpack-plugin');
const path = require('path');

module.exports = (env, argv) => {
    const isProduction = argv.mode === 'production';

    return {
        mode: isProduction ? 'production' : 'development',
        entry: {
            main: './src/js/main.js',
        },
        output: {
            filename: '[name].js',
            path: path.resolve(__dirname, 'wwwroot/js/dist'),
            publicPath: '/js/dist/',
            clean: true,
        },
        module: {
            rules: [
                {
                    test: /\.css$/i,
                    use: ['style-loader', 'css-loader'],
                },
                {
                    test: /\.js$/,
                    exclude: /node_modules/,
                    use: {
                        loader: 'babel-loader',
                        options: {
                            presets: [
                                ['@babel/preset-env', {
                                    targets: '> 0.25%, not dead',
                                    modules: false,
                                    useBuiltIns: 'usage',
                                    corejs: 3,
                                }],
                            ],
                        },
                    },
                },
            ],
        },
        resolve: {
            extensions: ['.js', '.mjs'],
        },
        optimization: {
            splitChunks: false,
            runtimeChunk: false,
            minimize: isProduction,
            minimizer: isProduction ? [
                new TerserPlugin({
                    terserOptions: {
                        ecma: 2020,
                        compress: {
                            drop_console: true,
                            passes: 3,
                        },
                        mangle: {
                            toplevel: true,
                        },
                        format: {
                            comments: false,
                        },
                    },
                    extractComments: false,
                }),
            ] : [],
        },
        devtool: isProduction ? false : 'eval-source-map',
        performance: {
            hints: isProduction ? 'warning' : false,
        }
    };
};
