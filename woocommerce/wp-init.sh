#!/usr/bin/env bash
# Seed script for the bundled WP+WC showcase stack. Idempotent — safe to re-run.
set -euo pipefail

# Wait for WP to be reachable before invoking wp-cli (the wordpress container starts
# nginx/php-fpm before any DB schema exists; first request triggers the install path).
echo "[wp-init] waiting for WordPress to become reachable on http://wc-wordpress..."
for i in {1..30}; do
  if wp --path=/var/www/html core is-installed --allow-root 2>/dev/null; then
    echo "[wp-init] WordPress already installed."
    break
  fi
  if curl -fsS http://wc-wordpress 2>/dev/null | grep -qi 'wordpress\|WooCommerce'; then
    echo "[wp-init] WordPress responding; running install."
    break
  fi
  sleep 2
done

if ! wp --path=/var/www/html core is-installed --allow-root 2>/dev/null; then
  echo "[wp-init] running wp core install..."
  wp --path=/var/www/html core install \
    --url='http://localhost:8080' \
    --title='Acme Ship Showcase' \
    --admin_user=admin \
    --admin_password=admin \
    --admin_email=admin@local.test \
    --skip-email \
    --allow-root
fi

echo "[wp-init] ensuring WooCommerce plugin..."
wp --path=/var/www/html plugin install woocommerce --activate --allow-root

echo "[wp-init] seeding store address (NL) so PostNL labels make sense..."
wp --path=/var/www/html option update woocommerce_store_address 'Damrak 70' --allow-root
wp --path=/var/www/html option update woocommerce_store_city 'Amsterdam' --allow-root
wp --path=/var/www/html option update woocommerce_store_postcode '1012 LM' --allow-root
wp --path=/var/www/html option update woocommerce_default_country 'NL' --allow-root
wp --path=/var/www/html option update woocommerce_currency 'EUR' --allow-root
wp --path=/var/www/html option update woocommerce_weight_unit 'kg' --allow-root
wp --path=/var/www/html option update woocommerce_dimension_unit 'mm' --allow-root

echo "[wp-init] enabling REST API + permalinks (REST needs pretty permalinks)..."
wp --path=/var/www/html rewrite structure '/%postname%/' --allow-root
wp --path=/var/www/html rewrite flush --allow-root
wp --path=/var/www/html option update woocommerce_api_enabled 'yes' --allow-root

echo "[wp-init] done. Open http://localhost:8080/wp-admin (admin / admin)."
