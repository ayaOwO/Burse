module.exports = {
  '/api/**': {
    target: process.env.BURSE_API_URL || 'http://localhost:5243',
    secure: false,
    pathRewrite: { '^/api': '' },
  },
};
