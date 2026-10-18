'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {SealGame, SealJudge} = require('./game-engine.js');

const source = JSON.parse(fs.readFileSync(path.join(__dirname, '../Tools/SealChart.json'), 'utf8'));
const sampleRate = source.sampleRate, bpm = source.bpm;
const actionValue = {nod:0,diantou:0,left:1,right:2,roll:3};
const rawNotes = [...source.notes, ...source.introDemonstrations].sort((a,b) => a.beat - b.beat);
let phase = false, nextLeft = false;
const notes = rawNotes.map(note => {
  const action = actionValue[note.action];
  const result = {sample:Math.round(note.beat * 60 / bpm * sampleRate), action, input:action === 3 ? nextLeft ? 1 : 2 : action, phaseBefore:phase, nextLeftBefore:nextLeft};
  if (action !== 0) nextLeft = !nextLeft;
  if (action === 3) phase = !phase;
  return result;
});
const data = {sampleRate,bpm,clipSamples:source.clipSamples,notes,introSamples:notes.filter(note => note.action === 0).map(note => note.sample),assets:{music:'data:audio/mpeg;base64,AA=='}};
class FakeContext {
  constructor() { this.currentTime = 0; this.state = 'suspended'; this.destination = {}; this.sources = []; }
  async resume() { this.state = 'running'; }
  async close() { this.state = 'closed'; }
  decodeAudioData(bytes, success) { const buffer = {duration:data.clipSamples/sampleRate}; success(buffer); return Promise.resolve(buffer); }
  createBufferSource() {
    const source = {connect(){},disconnect(){},start(...args){this.started=args;},stop(){this.stopped=true;}};
    this.sources.push(source); return source;
  }
}
function fixture(storage) {
  const audio = new FakeContext();
  let wall = 0;
  const game = new SealGame(data, {audioContextFactory:() => audio, storage:storage || null, wallClock:() => wall});
  return {game,audio,setWall:value => {wall=value;}};
}
function runNote(game, audio, note, input) {
  audio.currentTime = game._songStart + note.sample / sampleRate - game._segmentOffset;
  game.tick();
  return game.press(input == null ? note.input : input);
}
async function runSong(practice) {
  const {game,audio} = fixture();
  assert.equal(game.dialogue, '开始准备，根据节拍跟上队伍的动作');
  assert.equal(await game.start(practice), true);
  assert.equal(game.state, 'countin'); assert.equal(game.countdown, 4);
  assert.equal(game.total, practice ? 23 : 235);
  assert.equal(audio.sources[0].started[0], game._songStart);
  const activeNotes = [...game.notes];
  let nods = 0, lefts = 0, rights = 0, rolls = 0;
  for (const note of activeNotes) {
    const result = runNote(game,audio,note);
    assert.equal(result.grade,'Perfect');
    assert.equal(game.captain.input,note.input);
    assert.equal(game.captain.pose,note.input === 0 ? 3 : note.input === 1 ? 1 : 2);
    const player = game.actors[2], partner = game.actors[0];
    assert.equal(player.motion,partner.motion);
    assert.equal(player.phase,partner.phase);
    if (note.action === 0) { nods++; assert.equal(player.motion,1); }
    if (note.action === 1) lefts++;
    if (note.action === 2) rights++;
    if (note.action === 3) rolls++;
  }
  audio.currentTime = game._songStart + game.duration - game._segmentOffset;
  game.tick();
  assert.equal(game.state,'results'); assert.equal(game.judge.perfect,activeNotes.length);
  assert.equal(game.judge.score,1000000); assert.equal(game.judge.stray,0); assert.equal(game.judge.miss,0);
  assert.equal(game.captain.beatCueCount,activeNotes.length);
  if (!practice) { assert.deepEqual([nods,lefts,rights,rolls],[5,84,84,62]); assert.equal(game.best,1000000); }
  game.back(); assert.equal(game.state,'ready'); assert.equal(game.captain.beatCueCount,0);
  game.destroy();
}

(async () => {
  // Exact inclusive boundaries, nearest-note ties, direction errors, and duplicate presses.
  const judged = [];
  const judge = new SealJudge([{sample:44100,input:0},{sample:88200,input:1},{sample:132300,input:2}],44100,false,result => judged.push(result));
  assert.equal(judge.submit(0,44100 - .055*44100).grade,'Perfect');
  assert.equal(judge.submit(0,44100).grade,'Stray');
  assert.equal(judge.submit(2,88200).grade,'Miss');
  assert.equal(judge.submit(2,132300 + .12*44100).grade,'Good');
  assert.deepEqual([judge.perfect,judge.good,judge.miss,judge.stray,judge.resolvedCount],[1,1,1,1,3]);
  assert.equal(judge.score,Math.round(1.65/3.25*1e6));
  const tied = new SealJudge([{sample:1000,input:1},{sample:1200,input:2}],44100,false);
  assert.equal(tied.submit(2,1100).noteIndex,0);
  assert.equal(tied.miss,1);
  const expiry = new SealJudge([{sample:1000,input:0}],1000,false);
  expiry.advance(1120); assert.equal(expiry.miss,0);
  expiry.advance(1121); assert.equal(expiry.miss,1);
  const practiceJudge = new SealJudge([{sample:1000,input:0}],1000,true);
  assert.equal(practiceJudge.submit(0,1180).grade,'Good');
  await runSong(false); await runSong(true);

  const {game,audio,setWall} = fixture();
  game.press(0); assert.equal(game.actors[2].motion,1); assert.equal(game.actors[2].frame,0);
  setWall(1); game.tick(); assert.equal(game.actors[2].frame,20); assert.equal(game.judge.stray,0);
  await game.start(false);
  const oldStart = game._songStart;
  audio.currentTime = .7; game.tick();
  const count = game.countdown;
  game.pause(); const remaining = game._remainingCountIn;
  audio.currentTime += 10; game.tick(); assert.equal(game.countdown,count); assert.equal(game.time,0);
  await game.resume();
  assert.equal(game._songStart,audio.currentTime+.04+remaining);
  assert.notEqual(game._songStart,oldStart);
  for (const note of game.notes.slice(0,5)) runNote(game,audio,note);
  assert.equal(game.judge.perfect,5); assert.equal(game.judge.stray,0);
  audio.currentTime = game._songStart + 6.1; game.tick(); game.pause();
  const held = game.time, playerFrame = game.actors[2].frame;
  audio.currentTime += 31; game.tick(); assert.equal(game.time,held); assert.equal(game.actors[2].frame,playerFrame);
  await game.resume();
  assert.equal(audio.sources.at(-1).started[1],held);
  game.tick(); assert.equal(game.time,held, 'Resume scheduling must hold the sample clock until sound starts');
  const sourceStart = game._songStart;
  audio.currentTime = sourceStart + .4; game.tick(); assert.ok(Math.abs(game.time-held-.4)<1e-9);
  // An OS audio interruption pauses rather than skipping chart events when it resumes.
  audio.state = 'interrupted'; audio.onstatechange(); assert.equal(game.state,'paused');
  audio.currentTime += 100; game.tick(); assert.equal(game.state,'paused');
  await game.resume(); assert.equal(game.state,'playing');
  game.setOffset(999); assert.equal(game.offset,250);
  game.setOffset(-999); assert.equal(game.offset,-250);
  game.back(); await game.start(false); game.setOffset(0);
  const roll = game.notes.find(note => note.action === 3);
  audio.currentTime = game._songStart + roll.time - 1.5 * game.beatDuration; game.tick();
  assert.equal(game.dialogue,'准备，翻滚一次！');
  const result = runNote(game,audio,roll,0);
  assert.equal(result.grade,'Miss'); assert.equal(game.actors[2].motion,1); assert.equal(game.actors[2].phase,false);
  game.back(); await game.start(false);
  const first = game.notes[0];
  assert.equal(runNote(game,audio,first,1).grade,'Miss');
  assert.equal(runNote(game,audio,first,0).grade,'Stray');
  // Correct input restores canonical state after the wrong-direction roll/extra press.
  const afterRoll = game.notes.find(note => note.time > roll.time);
  const recovery = runNote(game,audio,afterRoll);
  assert.equal(recovery.grade,'Perfect'); assert.equal(game.actors[2].phase,afterRoll.phaseBefore);
  game.destroy();
  const deniedStorage = {getItem(){throw new Error('denied');},setItem(){throw new Error('denied');}};
  const safe = fixture(deniedStorage); safe.game.setOffset(20); safe.game.toggleAssist(); safe.game.destroy();
  console.log('PASS: full 235/235 and practice 23/23 Perfect; 5 opening nods; nearest/strict/inclusive judge; preview, countdown pause, resume offset, OS interruption, wrong-key recovery, Chinese dialogue, denied storage.');
})().catch(error => { console.error(error); process.exitCode = 1; });
