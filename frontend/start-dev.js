process.env.BROWSER = 'none';
process.env.CI = 'false';
process.chdir(__dirname);
require('./node_modules/react-scripts/scripts/start');
