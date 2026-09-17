const fs = require('fs');
const path = require('path');
const dir = path.resolve(__dirname, 'fixtures/previous-arrowvortex');
const rate = 44100, seconds = 34, samples = rate * seconds;
const pcm = new Float64Array(samples);
// Original 120 BPM percussion exercise. First beat is at 2 s; no external music.
for (let beat = 0; beat < 60; beat++) {
  const onset = 2 + beat * .5;
  for (let i = 0; i < rate * .16; i++) {
    const t = i / rate;
    const pitch = beat % 4 === 0 ? 190 : 420;
    const tone = .38 * Math.sin(2 * Math.PI * pitch * t) * Math.exp(-t * 35);
    const kick = beat % 2 === 0 ? .25 * Math.sin(2 * Math.PI * (65 * t + .8 * (1-Math.exp(-30*t)))) * Math.exp(-t * 25) : 0;
    pcm[Math.round(onset * rate) + i] += tone + kick;
  }
}
const wav = Buffer.alloc(44 + samples * 2);
wav.write('RIFF'); wav.writeUInt32LE(wav.length - 8, 4); wav.write('WAVEfmt ', 8);
wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
wav.writeUInt32LE(rate, 24); wav.writeUInt32LE(rate * 2, 28); wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
wav.write('data', 36); wav.writeUInt32LE(samples * 2, 40);
for (let i = 0; i < samples; i++) wav.writeInt16LE(Math.round(Math.max(-1, Math.min(1, pcm[i])) * 32767), 44 + i*2);
fs.writeFileSync(path.join(dir, 'first-beat.wav'), wav);
console.log('Original exercise audio: 34 seconds, 120 BPM, first beat at 2 seconds.');
