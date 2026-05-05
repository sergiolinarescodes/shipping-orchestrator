<?php
/**
 * Plugin Name: Ship Shipping — enable WP_DEBUG_LOG (DEV ONLY)
 * Description: Forces WP_DEBUG + WP_DEBUG_LOG so HTTP-layer errors land in
 *   wp-content/debug.log. Required for diagnosing the WC /wc-auth callback failures.
 *
 * Same SHIPPING_DEV_LOCAL gate as the SSL-bypass mu-plugin: no-op unless the
 * local compose stack set the constant, so the file alone can't accidentally
 * enable debug logging in a real install.
 */
if (!defined('ABSPATH')) exit;
if (!defined('SHIPPING_DEV_LOCAL') || !SHIPPING_DEV_LOCAL) return;
if (!defined('WP_DEBUG')) define('WP_DEBUG', true);
if (!defined('WP_DEBUG_LOG')) define('WP_DEBUG_LOG', true);
if (!defined('WP_DEBUG_DISPLAY')) define('WP_DEBUG_DISPLAY', false);
@ini_set('log_errors', '1');
@ini_set('error_log', '/var/www/html/wp-content/debug.log');

// Trace any WP_Error returned by HTTP requests so we see WHY WC said "An error occurred".
add_action('http_api_debug', function ($response, $context, $class, $args, $url) {
    if (is_wp_error($response)) {
        error_log(sprintf(
            '[ship-shipping] http_api_debug WP_Error to %s : %s : %s',
            $url,
            $response->get_error_code(),
            $response->get_error_message()
        ));
    } else {
        $code = wp_remote_retrieve_response_code($response);
        $body = wp_remote_retrieve_body($response);
        error_log(sprintf(
            '[ship-shipping] http_api_debug to %s -> %s body=%s',
            $url,
            $code,
            mb_substr((string)$body, 0, 800)
        ));
    }
}, 10, 5);
