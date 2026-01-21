# Loxone Miniserver Communication Guide (HTTPS + WebSocket)

Based primarily on **“Communicating with the Loxone Miniserver” (v16.0, 2025‑06‑03)**. citeturn1view0

> Notes:
> - Miniservers of the newer generation support **HTTPS/WSS**. citeturn8view2turn8view3  
> - Some older firmware versions may still require *application-layer encryption* for specific commands even when TLS is used. citeturn6view0

---

## 1) Decide how you will reach the Miniserver

### A. Local network (LAN)
Use the Miniserver’s local IP/hostname.

### B. Remote (internet) via CloudDNS / Remote Connect
You can resolve the current reachable address using CloudDNS:

- `dns.loxonecloud.com/?getip&snr={serial}&json=true` citeturn8view4  
  The response includes status `Code` values and connection attributes such as `IP`, `PortOpen`, and (for TLS-capable Miniservers) `IPHTTPS` / `PortOpenHTTPS`. citeturn8view4turn8view3

For globally distributed Remote Connect datacenters, the getip response provides a `DataCenter` attribute, and Loxone recommends using it for faster connection establishment. citeturn8view5

---

## 2) Check whether HTTPS/WSS is supported (and pick the correct base URL)

If you can reach the Miniserver locally, do:

- `http://{miniserverIP}/jdev/cfg/apiKey` citeturn8view3turn8view0  

The response contains:
- `httpsStatus`:
  - `1` = TLS protocols available
  - `2` = certificate present but expired citeturn8view3
- Since **12.1**, a `local` attribute indicating whether the Miniserver considers the connection “local” (relevant for access restrictions). citeturn8view0turn8view2

**If TLS is supported**:
- Prefer:
  - `https://{hostname}:{port}/...`
  - `wss://{hostname}:{port}/ws/rfc6455`
- Loxone advises using a **hostname** (not IP) for TLS because certificates cannot be created for IP addresses. citeturn8view3turn8view2

**Connecting locally with TLS using CloudDNS hostname**
Loxone describes creating a hostname that looks like a remote connection but points to your local IP/port (example shown in the document). citeturn8view4

---

## 3) Understand the two “layers” you may need

There are two distinct security mechanisms you may encounter:

1. **Transport encryption (TLS):** HTTPS/WSS  
2. **Application-layer command encryption:** Loxone “Command Encryption” using AES + RSA key exchange. citeturn6view3turn6view4

If you are on a TLS-capable Miniserver (Gen2/Compact), TLS may be enough for many cases. citeturn6view0turn8view2  
However, the v16.0 document still describes application-layer encryption and states that some older versions required it for certain operations even with TLS. citeturn6view0turn6view3

---

## 4) Tokens: the authentication you should build for

Password-based authentication was removed in favor of **token-based authentication**; **JWT** support was introduced and legacy tokens are deprecated. citeturn1view0turn5view1

### Permissions matter
When acquiring a token you must request a permission level; the document highlights that at least **Web (2)** or **App (4)** permissions are important for communication. citeturn5view1

---

# Part A — HTTPS communication (requests + responses)

## A1) The basic HTTP request shape

Most endpoints are sent as paths like:

- `.../jdev/...` (JSON format; responses may use `dev` in the echoed “control”) citeturn9view0

Example control command path pattern:

- `jdev/sps/io/{uuid}/{command}` citeturn9view0

> Tip: The document warns that “command responses” are not always fully implemented, so for real state you should rely on state updates (WebSocket). citeturn9view0

## A2) Auth on HTTP requests (token)

The token authentication command is appended as query parameters:

- Append `?autht={hash}&user={user}` to your existing cmd. citeturn5view1

Where:
- `{hash}` is derived from `{token}` and a key returned by a getkey request (hash algorithm HMAC-SHA1 or HMAC-SHA256 per document), and
- starting with **11.2** the token can also be sent in plaintext instead of a hash. citeturn5view1

## A3) When you must use application-layer command encryption over HTTP

The document provides a full “Step-by-step Guide HTTP Requests” for encrypting commands. citeturn6view3turn6view4  
This is the workflow **in plain English**:

1) Fetch Miniserver public key: `jdev/sys/getPublicKey` → `{publicKey}` citeturn5view0turn6view3  
2) Prepare your command `{cmd}` (including token auth parameters if needed). citeturn5view0turn6view3  
3) Generate a random salt `{salt}` (hex string). citeturn5view0turn6view4  
4) AES-encrypt a command envelope that includes the salt. citeturn6view4  
5) Send encrypted command using:
   - `jdev/sys/enc/{enc-cipher}` (only command encrypted), or
   - `jdev/sys/fenc/{enc-cipher}` (response also AES-encrypted). citeturn6view4  
6) RSA-encrypt `{key}:{iv}` with `{publicKey}` and append it as `?sk={enc-session-key}`. citeturn6view4turn6view3  
7) Send the HTTP request. If decryption fails, Miniserver can respond with `401`. citeturn6view4

---

# Part B — WebSocket communication (commands + state updates)

## B1) Open the WebSocket

Connect to:

- `ws://{ipOrUrl}:{port}/ws/rfc6455` citeturn8view1  
- If supported, use **WSS** instead. citeturn8view1turn8view2  
- Set header: `Sec-WebSocket-Protocol: remotecontrol` citeturn8view1

## B2) Negotiate application-layer encryption (session key exchange)

The v16.0 step-by-step guide for WebSocket setup includes a key exchange: citeturn8view1turn8view0

1) Fetch and verify certificate chain: `jdev/sys/getcertificate` citeturn8view0turn8view1  
2) Extract public key from the last certificate → `{publicKey}` citeturn8view1  
3) Generate AES key `{key}` (AES256-CBC, hex) and IV `{iv}` (16 bytes, hex). citeturn8view1  
4) RSA-encrypt `{key}:{iv}` with `{publicKey}` → `{encrypted-session-key}` (Base64). citeturn8view1  
5) Send `jdev/sys/keyexchange/{encrypted-session-key}` over the WebSocket. citeturn8view1  
6) Generate a random `{salt}` (hex). citeturn8view1  
7) From here you either:
   - authenticate with an existing token, or
   - acquire a new token. citeturn8view1turn5view1  
8) Update salt frequently (document suggests after every sent command) to prevent replay attacks. citeturn8view1turn6view4

## B3) Acquire a token (JWT)

Token acquisition is described as a multi-step process: citeturn5view1turn6view5

1) Get key + userSalt + hashAlg:
- `jdev/sys/getkey2/{user}` → `{key}`, `{userSalt}`, `{hashAlg}` citeturn5view1turn6view5  

2) Hash password with salt:
- `{pwHash}` = HASH( `"{password}:{userSalt}"` using `{hashAlg}` ), result uppercased citeturn5view1turn6view5  

3) Create `{hash}` = HMAC( `"{user}:{pwHash}"` with `{key}`  
The document notes: *do not change case of the result*. citeturn6view5  

4) Request JWT (must be encrypted):
- `jdev/sys/getjwt/{hash}/{user}/{permission}/{uuid}/{info}` citeturn6view5turn5view1  
  - Must be encrypted; unencrypted calls are rejected with **400 Bad Request**. citeturn6view5  
  - `{permission}` influences lifespan (web=2 short; app=4 weeks). citeturn6view5turn5view1  
  - `{uuid}` identifies the client. citeturn6view5  

The response contains `{token}` (JWT), `{validUntil}`, `{tokenRights}`, and other attributes. citeturn5view1

## B4) Authenticate an existing WebSocket session using a token

- `authwithtoken/{hash}/{user}` citeturn5view1

Starting with **11.2** the token can also be sent in plaintext instead of a hash. citeturn5view1  
Once a token is acquired successfully on a WebSocket connection, Loxone considers that connection authenticated. citeturn5view1

---

## B5) Send commands over WebSocket (plain vs encrypted)

### Plain command frames
After authentication you can send:
- `jdev/sps/io/{uuid}/{command}` citeturn9view0

### Encrypted command frames (enc / fenc)
1) AES-encrypt `salt/{salt}/{cmd}` → `{cipher}` (Base64) citeturn6view4  
2) URI-encode `{cipher}` → `{enc-cipher}` citeturn6view4  
3) Send:
   - `jdev/sys/enc/{enc-cipher}`, or
   - `jdev/sys/fenc/{enc-cipher}` citeturn6view4  
4) If you used `fenc`, AES-decrypt the response with `{key}` / `{iv}`. citeturn6view4

---

## B6) Listen for responses: how WebSocket messages are structured

Each payload is preceded by an **8-byte binary header**. citeturn6view1turn1view0

### Header (8 bytes)
- Byte 1: `0x03`
- Byte 2: Identifier
- Byte 3: Flags (e.g. Estimated)
- Byte 4: reserved
- Bytes 5–8: payload length (uint32, little-endian) citeturn6view1

### Identifier values
| Identifier | Payload type |
|---:|---|
| 0 | Text-Message |
| 1 | Binary File |
| 2 | Event-Table of Value-States |
| 3 | Event-Table of Text-States |
| 4 | Event-Table of Daytimer-States |
| 5 | Out-of-Service indicator |
| 6 | Keepalive response |
| 7 | Event-Table of Weather-States | citeturn6view1

### Receive loop pattern
1) Read binary header (8 bytes)  
2) Read the next frame (payload)  
3) Dispatch by identifier (0=text JSON; 2/3/4/7=binary event tables; 6=keepalive OK; 5=reconnect) citeturn6view1turn10view0

---

## B7) Enable and parse state updates

To receive live states, enable binary status updates:

- Send `jdev/sps/enablebinstatusupdate` citeturn1view0turn0search1  

Then the Miniserver sends initial Event-Tables (Value/Text/Daytimer/Weather), and later only changes. citeturn1view0turn0search1

Binary layouts for these tables are described in the document. citeturn10view0turn10view1turn6view2

---

## B8) Keep the connection alive

If the client sends nothing for more than 5 minutes, the Miniserver closes the connection.  
Use `keepalive`; the Miniserver responds with identifier **6**. citeturn6view1turn5view1

---

# Part C — Troubleshooting (including “Policy Not Fulfilled”)

## C1) Issues called out in the v16.0 document
- Too many failed login attempts → block; WebSocket closes with **4003**. citeturn8view2  
- External connection when only local allowed → check `local` in `/jdev/cfg/apiKey`. citeturn8view2turn8view0  
- Limited number of concurrent event slots → check `hasEventSlots`. citeturn8view2  

## C2) “Policy Not Fulfilled” (Code 420)

Older official PDFs document that if you don’t authenticate quickly after opening the socket, you can get **420** and the socket closes. citeturn7search0  

Community traces show:
- `{"LL":{"control":"Auth","value":"Policy Not Fulfilled","Code":"420"}}` citeturn7search1  

### Likely causes
1) You opened the socket but didn’t finish the auth flow fast enough. citeturn7search0turn7search1  
2) You tried a call that must be encrypted (e.g. `getjwt`) without using command encryption. citeturn6view5  
3) Remote/local access policy restriction (`local` flag false). citeturn8view2turn8view0  

### Quick checklist
- `Sec-WebSocket-Protocol: remotecontrol` set? citeturn8view1  
- Keyexchange done (if using enc/fenc auth flow)? citeturn8view1  
- `getjwt` is encrypted? citeturn6view5  
- Connection considered local if required? citeturn8view0turn8view2  

---

## Appendix: Minimal end-to-end sequence (typical)

1. `https://{host}:{port}/jdev/cfg/apiKey` → check `httpsStatus`/`local` citeturn8view3turn8view0  
2. Open `wss://{host}:{port}/ws/rfc6455` with `remotecontrol` citeturn8view1turn8view2  
3. Keyexchange + encrypted `getjwt` to obtain token (first time) citeturn8view1turn6view5  
4. `authwithtoken/...` citeturn5view1  
5. `jdev/sps/enablebinstatusupdate` and parse event tables citeturn1view0turn10view0  
6. Send control commands `jdev/sps/io/{uuid}/{command}` citeturn9view0  
7. Send `keepalive` periodically citeturn6view1turn5view1  

---

## References
- Loxone PDF: “Communicating with the Loxone Miniserver” (v16.0, 2025‑06‑03). citeturn1view0  
- Loxforum thread showing “Policy Not Fulfilled” / Code 420 in Auth response. citeturn7search1  
