# Setting up a Connection

## What do we need?

-   A WebSocket client implementation conforming to **RFC6455**
-   The IP or URL of the Miniserver (including port)
    -   If using Loxone CloudDNS, resolve the address first
-   Valid credentials (username & password)

## Step-by-step Guide

This guide uses encryption + token authentication.

### 1. Ensure the Miniserver is reachable

Request:

{ipOrUrl}:{port}/jdev/cfg/apiKey

Response includes MAC address, config version, httpsStatus, and local
attribute.

### 2. Acquire the certificate

Request:

jdev/sys/getcertificate

Verify certificate chain and extract public key.

### 3. Open WebSocket connection

ws://{ipOrUrl}:{port}/ws/rfc6455

Subprotocol:

Sec-WebSocket-Protocol: remotecontrol

### 4. Generate encryption session

Generate AES-256 key and IV.

### 5. Encrypt session key

Encrypt "{key}:{iv}" with RSA public key.

### 6. Send key exchange

jdev/sys/keyexchange/{encrypted-session-key}

### 7. Generate salt

Random hex salt. Update after each command.

### 8. Authenticate

Use existing token or acquire a new one.

### 9. Socket ready

After authentication the socket is ready.

## What can go wrong?

-   Invalid credentials → 401
-   No auth in time → 420
-   Commands before auth → 400
-   Too many failed logins → blocked
-   Event slots full → no live updates

## Using HTTPS / WSS

New Miniservers support TLS.

Use HTTPS/WSS and hostname instead of IP.

Check TLS support via:

http://{ip}/jdev/cfg/apiKey
