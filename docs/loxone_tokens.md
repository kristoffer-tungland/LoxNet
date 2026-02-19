Tokens
Available since 9.0, Updated in 10.2
Clients initially acquire a token using the users password. This token is stored and used instead of
the password for authentication. The following section will go into detail on how to work with
tokens.
In order to simplify authentication/verification throughout the Loxone Smart Home, JSON Web
Tokens have been introduced in version 10.2. In order to avoid breaking changes, version 10.2
introduces new web services for acquiring and refreshing JSON Web Tokens and a separate web
service allowing to check if tokens are valid.
Acquiring, refreshing and authenticating with legacy tokens is still supported, but deprecated.
Support for legacy tokens will be removed in future versions. It is highly recommended to move to
JSON Web Tokens as soon as possible. Legacy tokens may be converted to JSON Web Tokens using
the new refresh token command.
Starting with version 11.2 it is not mandatory anymore to use Encryption when acquiring tokens.
But it is highly recommended when communicating with a Miniserver without a transport layer
encryption!
Acquiring tokens
Updated in 10.2
Acquiring a token is similar to password authentication in previous versions. Additionally to the
“key”, a “salt” is needed for acquiring a token. A token can be either acquired via HTTP requests or
via a websocket.
● Acquire the “key”, “salt” & “hashAlg” at once using “jdev/sys/getkey2/{user}”
○ {user} is the username for whom to acquire the token.
○ The “salt” retrieved will be referred to as {userSalt}
○ “hashAlg” is the hashing algorithm that should be used
● Hash the password including the user specific salt
○ {pwHash} is the uppercase result of hashing the string “{password}:{userSalt}” using
the in the getkey2 command specified hashing algorithm (‘hashAlg’, e.g. SHA1,
SHA256).
○ {userSalt} is part of the result of the getkey2-Request.
16.0
Communicating with the Miniserver Page 29 of 36
● Create the hash that includes the user name
○ {hash} is the string “{user}:{pwHash}” hashed with the key returned by the getkey2-
Request using HMAC-SHA1 or HMAC-SHA256.
Do not convert the result to upper or lower case, leave it unchanged. For details on
the hashing process see Hashing
○ If you want to use a hashed token instead, the getparameter ‘tokenHash=true’ has
to be appended to the request. (Supported since version 12.2.10.6)
● Request a JSON Web Token “jdev/sys/getjwt/{hash}/{user}/{permission}/{uuid}/{info}”
○ This request must be encrypted. Unencrypted getjwt requests will be declined with
400 Bad Request. See command encryption for more details.
○ {permission} specifies the permission this token needs to grant. This integer
impacts the tokens lifespan, e.g. a token with the web-permission (2) will last for a
short period of time, while a token with the app-permission (4) will last for weeks.
○ The {uuid} identifies the client who is requesting the token on the Miniserver. It
allows to look up all tokens a client has been granted. This is why the UUID should
either be derived from your devices identity information or generated automatically
and stored within the app. It has to be in the following format as this one:
“098802e1-02b4-603c-ffffeee000d80cfd”.
○ The {info} contains a (Url-Encoded) text describing the client, e.g.
“Thomas%20iPhone%20X”
○ As of version 12.2 you may use a token to acquire a new token. Required:
■ {hash} will then be a token-Hash instead of a password hash.
■ Append “?authHash=True” to the command
● Tells the MS to check token hashes instead of password hashes.
● Store the response, it contains info on the lifespan, the permissions granted with that token
and the JSON Web Token itself.
○ {token} is the JSON Web Token itself, it needs to be stored for authenticating.
○ {validUntil} represents the end of the tokens lifespan in seconds since 1.1.2009
○ {tokenRights} holds a bitmap, where a flag is set for each granted permission.
○ {unsecurePass} is set to true if a weak password is in place, it should result in a
prominent warning for the user, asking to immediately change the password.
○ {key} can be used for subsequent commands, just like a getkey-Result.
● A websocket connection on which a token was acquired successfully is considered
authenticated.
Authenticating using tokens
16.0
Communicating with the Miniserver Page 30 of 36
● Prepare the {hash}
○ {hash} is the outcome of hashing “{token}” along with the result of a getkey-
Request using the HMAC-SHA1 or HMAC-SHA256 algorithm. (see Hashing)
○ As of version 10.0 both JSON Web Tokens and legacy tokens may be used for
authentication.
○ Starting with version 11.2 the token can also be sent in plaintext instead of a hash
● Prepare the {authCmd}
○
“authwithtoken/{hash}/{user}” for websockets
○ For HTTP-Requests “?autht={hash}&user={user}” is appended to the existing cmd.
● Encrypt and send the {authCmd}
○ Details on encrypted commands via Websocket
○ Details on encrypted commands via HTTP-Requests
Refreshing tokens
Updated in 10.2
Tokens have a limited lifespan, depending on the permissions granted with them. When this
lifespan expires, tokens will no longer be valid. Refreshing a token will return a new token with the
same permissions and an extended lifespan.
When passing a legacy token to the new refresh token command, a JSON Web Token will be
returned instead.
● Send “jdev/sys/refreshjwt/{tokenHash}/{user}” via websocket (HTTP support not verified)
○ {tokenHash} is the outcome of hashing the {token} with the result of a getkey-
Request. (see Hashing)
○ {user} is the user whose token is to be refreshed
○ Starting with version 11.2 the token can also be sent in plaintext instead of a hash
This request will only succeed if the token is valid. If successful, the response will contain an
updated {validUntil}-value, an updated {unsecurePass}-flag and a new {token} attribute.
Checking if tokens are valid
Available since 10.0
This request was introduced to allow verifying a that a token is still valid, without renewing it as in
“refreshToken”.
16.0
Communicating with the Miniserver Page 31 of 36
● Send “jdev/sys/checktoken/{tokenHash}/{user}” via websocket (HTTP support not verified)
○ {tokenHash} is the outcome of hashing the {token} with the result of a getkey-
Request. (see Hashing)
○ {user} is the user whose token is to be refreshed
○ Starting with version 11.2 the token can also be sent in plaintext instead of a hash
This request will only succeed if the token is valid. If successful, the response will contain an
{validUntil}-value and an updated {unsecurePass}-flag. When changing passwords, this request can
be used to determine if the new password is secure, by checking the {unsecurePass}-flag of a
refresh-request afterwards
Killing tokens
Tokens can be explicitly invalidated (= “killed”) too. It is recommended to kill a token as soon as it
is no longer needed, as it helps keeping the Miniservers token storage clean.
● Send “jdev/sys/killtoken/{tokenHash}/{user}”
○ {tokenHash} is the outcome of hashing the {token} with the result of a getkey-
Request. (see Hashing) {user} is the user whose token is to be killed
○ Starting with version 11.2 the token can also be sent in plaintext instead of a hash
A killed token will no longer be usable.