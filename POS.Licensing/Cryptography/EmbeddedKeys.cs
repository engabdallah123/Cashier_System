namespace POS.Licensing.Cryptography;

/// <summary>
/// Holds the embedded RSA Public Key for client-side license verification.
/// The Private Key is NEVER stored in the client application.
/// </summary>
public static class EmbeddedKeys
{
    // Production 3072-bit RSA Public Key for POS Cashier System
    public const string PublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAw9shO9WcplIogUAki9st
jH8iPNYu7i3RoPC7ZOuNBErPx/ID1MtbPHtHO8kEBMvFRQj6CQB0YQ/lmC/DCNYX
FdxpF1nzeS1AXciKRHT6GPYV4suiIJmQKTtZ97MdTN9+yRL0lTAptuOjRnDEOu12
rKmmjz46ucCAZ2LXvReVWgIQu5jN1nzd9al9TlWhjL6zTmLjWZww4pEY5AHZ/mW2
YHFqkYQ/obYrHokw7vxDdNCXXigaZyAR44kvhEAtl7efoGw3DdKV4vhef1WHlXnP
VKaThojH7qxx8TtSiCz2QpXMWNlVhHqvRqYYBxsfxjbDtQbSHSvNdCjxQSXejMzx
LpXWQRu+93t9CRsw8GDN4pnwsZi2RZNRV99G6EFyALlWJ18QbUYFPw6SaUlvV2+6
kNmAZt7v7A1svkm1gpQ3FFk0nSpaCC4cTYWIaFgNIMYf9MucXjEfIF9SEenaT+4j
kaTNoXtW35ec5+GeLhHGitzqEkaN57D4empzLwfdI3c1AgMBAAE=
-----END PUBLIC KEY-----
""";
}
