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
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAvXU3Zc0iocwTBt9ruC/w
Z738PJ2BxoSC32c8x2jan9DspKQ9MoKvWEgPQvzopZ2Qh8TsMl6WRS72Y7pYSfIA
kUH0+Fr/s+FFqiFD+4xnZc6cf6hXX1VPzHRCGSJ2n3oseoG5P5WRqb3QYRFrqEQr
WipmEUSrHZnQhA/dpNjwU08pepy+buSrEKML3kjuaskLdpJA+1kXLHnXwtMdxP9C
nYbDVINe7ErFQKhWbzalVKhYq1t6O3B/89vWprUK46WLFxWF9cQWNLEW3y+axJ6N
yFLpDSl8sk5QpLYRtd2xwVqDcei3wqN27Zh65UXVIKOdFGAh1EmuQLK8vvFxlOvS
QgBiFPGJZ4Ny8fgr4gD81+Y3Z1mTo3s/H+fxJPOGGDInvrTmsRvlENmOF6EahZX6
6+fUs2vJMsWEg/KT3rHIZ46SOHZZzKgiOwPWz2NsN/VcVqkU29tcKb0sXCTdMHUs
ATPr0fvwudlxJfR9A/a6NjbiYnTY88GdBA3umPka4hmxAgMBAAE=
-----END PUBLIC KEY-----
""";
}
