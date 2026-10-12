export const environment = {
  production: true,
  api: {
    order: '/api/order',
    inventory: '/api/inventory',
    priceFeed: '/api/pricefeed',
    pricing: '/api/pricing',
    audit: '/api/audit',
    observability: {
      jaeger: 'http://localhost:16686',
      prometheus: 'http://localhost:9090',
      grafana: 'http://localhost:3000'
    }
  }
};
