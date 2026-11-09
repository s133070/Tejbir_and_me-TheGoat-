// OTP mod 37 + HMAC-SHA256, volledig in de browser.

const ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_';

// Hoofdletters, spatie -> _, accenten weg; andere tekens weigeren.
export function normalize(text) {
    const clean = text.normalize('NFD').replace(/[̀-ͯ]/g, '')
                      .toUpperCase().replace(/ /g, '_');
    const bad = [...clean].filter(ch => !ALPHABET.includes(ch));
    if (bad.length) throw new Error('Niet toegelaten tekens: ' + [...new Set(bad)].join(' '));
    return clean;
}

// Per teken sleutel optellen (+1) of aftrekken (-1), mod 37.
function shift(text, key, sign) {
    if (key.length < text.length) throw new Error('Sleutel is korter dan het bericht');
    return [...text].map((ch, i) => {
        const p = ALPHABET.indexOf(ch), k = ALPHABET.indexOf(key[i]);
        if (p < 0 || k < 0) throw new Error('Ongeldig teken in bericht of sleutel');
        return ALPHABET[(p + sign * k + 37) % 37];
    }).join('');
}

export const encrypt = (plain, key) => shift(normalize(plain), key.trim().toUpperCase(), +1);
export const decrypt = (cipher, key) => shift(cipher, key.trim().toUpperCase(), -1);

function hexToBytes(hex) {
    hex = hex.trim();
    if (!/^[0-9a-fA-F]{64}$/.test(hex)) throw new Error('Verwacht 64 hex-tekens (32 bytes)');
    return new Uint8Array(hex.match(/../g).map(b => parseInt(b, 16)));
}

const bytesToHex = buf => [...new Uint8Array(buf)].map(b => b.toString(16).padStart(2, '0')).join('');

// Werkt enkel via HTTPS of localhost.
async function hmacKey(hex) {
    return crypto.subtle.importKey('raw', hexToBytes(hex),
        { name: 'HMAC', hash: 'SHA-256' }, false, ['sign', 'verify']);
}

// MAC over afzender, ontvanger, lijnnummer, versleuteld onderwerp en ciphertext.
const macData = m => new TextEncoder().encode(
    [m.sender, m.recipient, m.keyLine, m.subject, m.body].join('|'));

export async function sign(m, hex) {
    return bytesToHex(await crypto.subtle.sign('HMAC', await hmacKey(hex), macData(m)));
}

// verify vergelijkt in constante tijd (geen timing-aanval).
export async function verify(m, hex) {
    return crypto.subtle.verify('HMAC', await hmacKey(hex), hexToBytes(m.mac), macData(m));
}
