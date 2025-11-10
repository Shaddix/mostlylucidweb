// webpack.config.js
const TerserPlugin = require('terser-webpack-plugin');
const MiniCssExtractPlugin = require('mini-css-extract-plugin');
const { WebpackManifestPlugin } = require('webpack-manifest-plugin');
const path = require('path');

module.exports = (env, argv) => {
    const isProduction = argv.mode === 'production';

    return {
        mode: isProduction ? 'production' : 'development',
        entry: { main: './src/js/main.js' },
        output: {
            filename: '[name].js',                    // keep main.js stable
            chunkFilename: '[name].[contenthash:10].js',     // ✅ split chunks
            assetModuleFilename: 'assets/[name].[contenthash:10][ext]',                   // ✅ images/fonts/etc
            path: path.resolve(__dirname, 'wwwroot/js/dist'),
            publicPath: '/js/dist/',
            module: true,
            clean: true,
            // hashFunction: 'xxhash64', // (default in recent webpack, fine to omit)
        },
        experiments:{ outputModule: true },

        module: {
            rules: [
                {
                    test: /\.css$/i,
                    use: [
                        isProduction ? MiniCssExtractPlugin.loader : 'style-loader',
                        'css-loader'
                    ],
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
                { test: /\.(png|jpe?g|gif|svg|webp|ico|woff2?|ttf|eot)$/i, type: 'asset' }, // ✅ hashed via assetModuleFilename
            ],
        },

        resolve: {
            extensions: ['.js', '.mjs'],
            alias: {
                '@mostlylucid/mermaid-enhancements$':
                    path.resolve(__dirname, 'node_modules/@mostlylucid/mermaid-enhancements/dist/index.min.js')
            }
        },

        optimization: {
            splitChunks: {
                chunks: 'all',
                minSize: 20000,
                maxSize: 100000,
                name: false,
            },
            runtimeChunk: 'single',                        // ✅ separate runtime (also hashed by filename rule)
            moduleIds: 'deterministic',                    // ✅ stable chunk ids across builds
            chunkIds: 'deterministic',
            minimize: isProduction,
            minimizer: isProduction ? [
                new TerserPlugin({
                    terserOptions: {
                        ecma: 2020,
                        compress: { drop_console: false, passes: 3, toplevel: true, pure_funcs: ['console.info','console.debug'] },
                        mangle: { toplevel: true },
                        format: { comments: false },
                    },
                    extractComments: false,
                }),
            ] : [],
        },

        plugins: [
            ...(isProduction ? [new MiniCssExtractPlugin({
                filename: '[name].[contenthash:10].css',     // ✅ CSS cache-busting
                chunkFilename: '[name].[contenthash:10].css'
            })] : []),
            new WebpackManifestPlugin({                    // ✅ map logical names → hashed files
                fileName: 'manifest.json',
                publicPath: '/js/dist/',
                writeToFileEmit: true
            })
        ],

        devtool: isProduction ? false : 'eval-source-map',
        performance: { hints: isProduction ? 'warning' : false }
    };
};
