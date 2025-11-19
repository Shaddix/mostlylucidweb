const path = require('path');

module.exports = {
  entry: './Scripts/site.js',
  output: {
    filename: 'site.js',
    path: path.resolve(__dirname, 'wwwroot/js'),
  },
  mode: 'development'
};
