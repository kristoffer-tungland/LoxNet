# Loxone Miniserver --- Connection & Authentication Guide

A practical developer-oriented Markdown version of the official
communication flow.

------------------------------------------------------------------------

## Requirements

-   WebSocket client conforming to **RFC6455**
-   Miniserver IP or hostname + port
-   Valid credentials
-   Ability to perform:
    -   RSA encryption
    -   AES‑256‑CBC encryption
    -   certificate validation

⚠ If using CloudDNS, resolve the hostname before opening WebSocket.
Redirects will not work.

------------------------------------------------------------------------

## Connection Overview

The secure connection flow:

1.  Reachability check
2.  Certificate retrieval
3.  WebSocket open
4.  AES session generation
5.  RSA key exchange
6.  Token authentication
7.  Salt rotation for encrypted commands

All authentication uses encrypted commands.

------------------------------------------------------------------------

## Step 1 --- Reachability Check

Request:

    http://{ipOrUrl}:{port}/jdev/cfg/apiKey

Response includes:

-   MAC address
-   config version
-   `httpsStatus`
-   `local` attribute (since 12.1)

`local = 1` means connection is treated as internal network.

------------------------------------------------------------------------

## Step 2 --- Retrieve Certificate

Request:

    jdev/sys/getcertificate

Process:

1.  Validate certificate chain
2.  Ensure root matches stored Loxone Root Certificate
3.  Extract public key from last certificate
4.  Store public key locally

Public key format:

-   X.509
-   PEM encoded

------------------------------------------------------------------------

## Step 3 --- Open WebSocket

    ws://{ipOrUrl}:{port}/ws/rfc6455

Headers:

    Sec-WebSocket-Protocol: remotecontrol

New generation Miniservers support:

    wss://

Only use WSS with hostname (not IP).

------------------------------------------------------------------------

## Step 4 --- Generate AES Session

Generate:

-   AES‑256 key (hex)
-   AES IV (16 bytes, hex)

```{=html}
<!-- -->
```
    key = random 32 bytes
    iv  = random 16 bytes

------------------------------------------------------------------------

## Step 5 --- RSA Encrypt Session

Payload:

    {key}:{iv}

Encrypt with server public key:

    RSA(payload) → Base64 encrypted session key

This session encrypts all future commands.

------------------------------------------------------------------------

## Step 6 --- Send Key Exchange

    jdev/sys/keyexchange/{encrypted-session-key}

Server now expects encrypted commands.

------------------------------------------------------------------------

## Step 7 --- Salt Handling

Generate random hex salt:

    salt = random hex string

Each encrypted command uses:

    AES("nextSalt/prevSalt/nextSalt/cmd")

After sending:

-   update salt
-   never reuse old salt

Prevents replay attacks.

------------------------------------------------------------------------

## Step 8 --- Authentication

Two paths:

### Existing token

Authenticate with stored token.

### New token

Acquire token using credentials.

After success:

✅ socket authenticated\
✅ commands allowed

------------------------------------------------------------------------

## Error Conditions

  Code   Meaning
  ------ -------------------------------
  400    command sent before auth
  401    invalid credentials
  420    auth timeout
  4003   blocked after failed attempts

420 happens if authentication is not completed quickly after connect.

------------------------------------------------------------------------

## Limits

-   Max 31 event clients
-   Gen1: 48 HTTP connections
-   New gen: 256 HTTP connections

`hasEventSlots` flag indicates availability.

------------------------------------------------------------------------

## HTTPS / WSS Support

New Miniservers support TLS.

Requirements:

-   use HTTPS/WSS
-   use hostname (not IP)
-   valid certificate

CloudDNS automatically provides certificate.

Custom certificates can be uploaded via Loxone Config.

------------------------------------------------------------------------

## Detect TLS Support

Local check:

    http://{ip}/jdev/cfg/apiKey

Look for:

    httpsStatus

Values:

-   1 → TLS available
-   2 → certificate expired

CloudDNS response may include:

-   IPHTTPS
-   PortOpenHTTPS

------------------------------------------------------------------------

## Debug Warning

During debugging:

-   breakpoints
-   slow crypto
-   blocking receive loops

can trigger 420 timeout.

Always:

-   start receive loop immediately
-   avoid blocking handshake
-   authenticate quickly

------------------------------------------------------------------------

## Final Notes

-   Always rotate salt
-   Never reuse session keys
-   Prefer token authentication
-   Avoid IP when using TLS
-   Keep receive loop running continuously

------------------------------------------------------------------------
