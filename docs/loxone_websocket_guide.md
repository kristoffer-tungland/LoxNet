# Connecting to a Loxone Miniserver via WebSocket

This document is based on **Communicating With the Loxone Miniserver**
by Loxone.

## Prerequisites

-   RFC6455 compatible WebSocket client
-   Miniserver IP/hostname and port
-   Valid credentials or token

## WebSocket Endpoint

-   WS: `ws://<miniserver-ip>:<port>/ws/rfc6455`
-   WSS: `wss://<hostname>:<port>/ws/rfc6455`

**Required subprotocol:** `remotecontrol`

## Connection Steps

1.  Fetch Miniserver certificate via HTTP (`jdev/sys/getcertificate`)
2.  Open WebSocket connection
3.  Perform key exchange (RSA → AES session key)
4.  Authenticate (token-based preferred)
5.  Enable state updates

## Enable State Updates

    jdev/sps/enablebinstatusupdate

## Notes

-   Messages are binary-framed
-   Use WSS where possible
-   Handle reconnects and close codes properly
