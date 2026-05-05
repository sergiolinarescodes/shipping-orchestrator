<?php
/**
 * Plugin Name: Ship Shipping — disable SSL verify for host.docker.internal (DEV ONLY)
 * Description: Skips TLS verification for outbound HTTP requests to the orchestrator
 *   running on the host machine via self-signed dev certs. Required because WC's
 *   `/wc-auth/v1/authorize` endpoint refuses non-HTTPS callback URLs, but the
 *   orchestrator's dev cert isn't trusted inside the WP container. NEVER ship this
 *   in production — it would silently accept any cert for that host.
 *
 * Activation gate: this file is a no-op unless the SHIPPING_DEV_LOCAL constant is
 * defined. That constant is set ONLY by `woocommerce/docker-compose.yml` via
 * WORDPRESS_CONFIG_EXTRA; any other WP install (real production, a copy-paste of
 * the file into another tree, etc.) will see the constant undefined and bail
 * before registering any of the unsafe filters. Belt + braces alongside the bind
 * mount being scoped to the local compose project.
 */

if (!defined('ABSPATH')) {
    exit;
}

if (!defined('SHIPPING_DEV_LOCAL') || !SHIPPING_DEV_LOCAL) {
    return;
}

// Spoof SSL for inbound REST API calls so WC's `is_ssl()` gate in
// WC_REST_Authentication accepts HTTP Basic / query-string auth. Production WC
// rejects credentials over HTTP because they'd be eavesdroppable, but in this
// self-contained dev stack the orchestrator + WP both live on the same machine
// and there's no real network hop. Scoped to /wp-json/ only so wp-admin keeps
// generating http:// URLs and merchants don't get redirected to a non-existent
// https://localhost:8080 when navigating the dashboard.
// DEV ONLY — remove before any non-loopback deployment.
if (isset($_SERVER['REQUEST_URI']) && strpos($_SERVER['REQUEST_URI'], '/wp-json/') === 0) {
    $_SERVER['HTTPS'] = 'on';
}

add_filter('http_request_args', function ($args, $url) {
    if (is_string($url)
        && (strpos($url, 'host.docker.internal') !== false
            || strpos($url, '://localhost') !== false)) {
        $args['sslverify'] = false;
        $args['reject_unsafe_urls'] = false;
    }
    return $args;
}, PHP_INT_MAX, 2);

// Belt + braces: override at the curl layer too. WC's WC_Auth::post_consumer_data hardcodes
// sslverify=true and the http_request_args filter doesn't always win against late merges in
// every WP version. Forcing the cURL handle's verify flags off catches anything the WP
// filter chain misses. Same host scope as above — only applies to dev-loop targets.
add_action('http_api_curl', function ($handle, $args, $url) {
    if (is_string($url)
        && (strpos($url, 'host.docker.internal') !== false
            || strpos($url, '://localhost') !== false)) {
        curl_setopt($handle, CURLOPT_SSL_VERIFYPEER, false);
        curl_setopt($handle, CURLOPT_SSL_VERIFYHOST, 0);
    }
}, PHP_INT_MAX, 3);

// Same for `wp_safe_remote_*` callers — WC uses the safe variants for webhooks.
add_filter('http_request_host_is_external', function ($is_external, $host) {
    if ($host === 'host.docker.internal' || $host === 'localhost') {
        return true;
    }
    return $is_external;
}, 10, 2);

// `wp_safe_remote_*` also gates on a port allow-list (default [80, 443, 8080]).
// The orchestrator listens on 5101 (HTTP) and 5111 (HTTPS) under the dev launch
// profiles, so without this filter every webhook delivery returns WP_Error
// ("invalid_http_api_port") with no log line — the caller treats it as a silent
// no-op and AS marks the action complete despite zero HTTP fired. Whitelist
// just our dev ports; production webhooks would target standard 80/443.
add_filter('http_allowed_safe_ports', function ($ports) {
    foreach ([5101, 5111] as $p) {
        if (!in_array($p, $ports, true)) {
            $ports[] = $p;
        }
    }
    return $ports;
});
