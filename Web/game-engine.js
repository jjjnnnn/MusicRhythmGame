/* Arctic Band: sample-clock rhythm engine. No network, workers or external libraries. */
(function (root) {
  'use strict';
  const NOD = 0, LEFT = 1, RIGHT = 2, ROLL = 3;
  const PREPARE = '开始准备，根据节拍跟上队伍的动作';
  const BEST_KEY = 'SealRhythm.SingleButton.v4.BestScore';
  const DEFAULT_DURATIONS = [4,20,48,48,46,46,42,41,46,48,43,43,20,48,48,46,46,42,42,46,46,43,43,1048,73,73,83,4,39,41];
  const clamp = (value, low, high) => Math.min(high, Math.max(low, value));
  const actionValue = value => typeof value === 'number' ? value : ({nod:NOD,diantou:NOD,left:LEFT,right:RIGHT,roll:ROLL})[String(value).toLowerCase()];
  const wallTime = () => typeof performance !== 'undefined' ? performance.now() / 1000 : Date.now() / 1000;

  /** Sample-clock judgement. Single-button input chooses an action, never its timing. */
  class SealJudge {
    constructor(notes, sampleRate, practice, onJudged) {
      this.notes = notes; this.sampleRate = sampleRate;
      this.perfectWindow = sampleRate * .055;
      this.goodWindow = sampleRate * (practice ? .180 : .120);
      this.onJudged = onJudged;
      this.resolved = new Uint8Array(notes.length);
      this.perfect = this.good = this.miss = this.stray = this.combo = this.maxCombo = this.resolvedCount = this.nextNoteIndex = 0;
    }
    get accuracy() { return this.notes.length ? (this.perfect + this.good * .65) / (this.notes.length + this.stray * .25) : 0; }
    get score() { return Math.round(this.accuracy * 1000000); }
    get totalNotes() { return this.notes.length; }
    advance(sample) {
      while (this.nextNoteIndex < this.notes.length) {
        const index = this.nextNoteIndex;
        const offset = sample - this.notes[index].sample;
        if (offset <= this.goodWindow) break;
        this.resolve(index, 'Miss', offset);
      }
    }
    submit(input, sample) {
      return this._submit(input, sample, false);
    }
    submitBeat(sample) {
      return this._submit(NOD, sample, true);
    }
    _submit(input, sample, automatic) {
      this.advance(sample);
      let nearest = -1, distance = Infinity, offset = 0;
      for (let i = this.nextNoteIndex; i < this.notes.length; i++) {
        if (this.resolved[i]) continue;
        const currentOffset = sample - this.notes[i].sample;
        if (currentOffset < -this.goodWindow) break;
        const currentDistance = Math.abs(currentOffset);
        if (currentDistance <= this.goodWindow && currentDistance < distance) {
          nearest = i; distance = currentDistance; offset = currentOffset;
        }
      }
      if (nearest >= 0) {
        if (automatic) input = this.notes[nearest].input;
        return this.resolve(nearest, this.notes[nearest].input !== input ? 'Miss' : distance <= this.perfectWindow ? 'Perfect' : 'Good', offset, input);
      }
      this.stray++; this.combo = 0;
      const result = {noteIndex:-1, grade:'Stray', offsetMs:0, input};
      if (this.onJudged) this.onJudged(result);
      return result;
    }
    resolve(index, grade, offset, input) {
      this.resolved[index] = 1; this.resolvedCount++;
      if (grade === 'Perfect') this.perfect++;
      else if (grade === 'Good') this.good++;
      else this.miss++;
      if (grade === 'Miss') this.combo = 0;
      else this.maxCombo = Math.max(this.maxCombo, ++this.combo);
      while (this.nextNoteIndex < this.notes.length && this.resolved[this.nextNoteIndex]) this.nextNoteIndex++;
      const result = {noteIndex:index, grade, offsetMs:offset * 1000 / this.sampleRate, input:input == null ? this.notes[index].input : input};
      if (this.onJudged) this.onJudged(result);
      return result;
    }
  }

  class SealGame {
    constructor(data, options) {
      options = options || {};
      this.data = data;
      this.sampleRate = data.sampleRate || 44100;
      this.bpm = data.bpm || 133;
      this.beatDuration = 60 / this.bpm;
      this.fullDuration = data.clipSamples / this.sampleRate;
      this.motionDurations = data.motionDurations || DEFAULT_DURATIONS;
      this._storage = options.storage;
      if (this._storage === undefined) { try { this._storage = root.localStorage; } catch (_) { this._storage = null; } }
      this._clock = options.wallClock || wallTime;
      this._audioContextFactory = options.audioContextFactory || (() => {
        const Constructor = root.AudioContext || root.webkitAudioContext;
        if (!Constructor) throw new Error('浏览器不支持音频播放，请使用 Safari 或 Chrome 打开此文件。');
        return new Constructor({latencyHint:'interactive'});
      });
      this._listeners = new Set();
      this._context = null; this._buffer = null; this._source = null; this._toneSources = [];
      this._token = 0; this._time = 0; this._songStart = 0; this._segmentOffset = 0;
      this._pausedState = 'playing'; this._remainingCountIn = 0;
      this._nextStage = 0; this._readyPlayed = false;
      this.loading = false; this.state = 'ready'; this.practice = false; this.error = '';
      this.assist = this._read('seal_h5_assist', '1') !== '0';
      this.offset = clamp(Number(this._read('seal_h5_offset', '0')) || 0, -250, 250);
      this.best = clamp(Number(this._read(BEST_KEY, '0')) || 0, 0, 1000000);
      let phase = false, nextLeft = false;
      this._allNotes = data.notes.map((source, index) => {
        const action = actionValue(source.action);
        if (!Number.isInteger(source.sample) || action == null || action < NOD || action > ROLL) throw new Error('关卡节拍数据无效');
        const before = source.phaseBefore == null ? phase : !!source.phaseBefore;
        const leftBefore = source.nextLeftBefore == null ? nextLeft : !!source.nextLeftBefore;
        const input = source.input == null ? (action === ROLL ? leftBefore ? LEFT : RIGHT : action) : actionValue(source.input);
        if (![NOD,LEFT,RIGHT].includes(input)) throw new Error('关卡按键数据无效');
        phase = action === ROLL ? !before : before;
        nextLeft = action === NOD ? leftBefore : !leftBefore;
        return {sample:source.sample, time:source.sample / this.sampleRate, action, input, phaseBefore:before, nextLeftBefore:leftBefore, sourceIndex:index};
      }).sort((a,b) => a.sample - b.sample || a.sourceIndex - b.sourceIndex);
      this._firstIntro = data.introSamples && data.introSamples.length ? data.introSamples[0] / this.sampleRate : this._allNotes.length ? this._allNotes[0].time : 0;
      this._dialogueCues = this._makeDialogue();
      this._resetActors(); this._buildJudge(false);
      this.judgement = null;
      this._visibilityHandler = () => {
        if (typeof document !== 'undefined' && document.hidden && this._isActive()) this.pause();
      };
      if (typeof document !== 'undefined') document.addEventListener('visibilitychange', this._visibilityHandler);
    }
    get time() { return this._time; }
    get total() { return this.notes.length; }
    get progress() { return this.duration ? clamp(this._time / this.duration, 0, 1) : 0; }
    get countdown() {
      if (this.state === 'countin') return clamp(Math.ceil((this._songStart - this._context.currentTime) / this.beatDuration), 1, 4);
      if (this.state === 'paused' && this._pausedState === 'countin') return clamp(Math.ceil(this._remainingCountIn / this.beatDuration), 1, 4);
      return 0;
    }
    get dialogue() {
      if (this.state === 'ready' || this.state === 'countin' || (this.state === 'playing' && this._time < this._firstIntro)) return PREPARE;
      if (this.state !== 'playing') return '';
      for (let i = this._dialogueCues.length - 1; i >= 0; i--) {
        const cue = this._dialogueCues[i];
        if (this._time >= cue.start && this._time < cue.end) return cue.text;
      }
      return '';
    }
    subscribe(callback) { this._listeners.add(callback); return () => this._listeners.delete(callback); }
    _emit() { for (const callback of this._listeners) callback(this); }
    _read(key, fallback) { try { const value = this._storage && this._storage.getItem(key); return value == null ? fallback : value; } catch (_) { return fallback; } }
    _save(key, value) { try { if (this._storage) this._storage.setItem(key, String(value)); } catch (_) { /* Local files and private browsing may deny storage. */ } }
    _isActive() { return this.state === 'playing' || this.state === 'countin'; }
    _buildJudge(practice) {
      this.practice = !!practice;
      this.duration = practice ? Math.min(this.fullDuration, 32 * this.beatDuration) : this.fullDuration;
      this.notes = this._allNotes.filter(note => note.time < this.duration - .15);
      this.judge = new SealJudge(this.notes, this.sampleRate, practice, result => this._onJudged(result));
    }
    _resetActors() {
      this.actors = Array.from({length:4}, (_, i) => ({motion:1, phase:false, upside:false, nextLeft:false, startPhase:false, startTime:-1, duration:this.motionDurations[1] / 60, frame:this.motionDurations[1], isPlayer:i === 2}));
      this.captain = {input:NOD, startTime:-1, pose:0, frame:0, beatCueCount:0};
    }
    async _ensureAudio() {
      if (!this._context || this._context.state === 'closed') {
        this._context = this._audioContextFactory();
        const context = this._context;
        context.onstatechange = () => {
          // The OS may interrupt audio without changing page visibility (calls, lock screen).
          if (context === this._context && context.state !== 'running' && this._isActive()) this.pause();
        };
      }
      const context = this._context;
      // Resume is invoked before the first await, while still inside the user's tap.
      const resume = context.state === 'running' ? Promise.resolve() : context.resume();
      await resume;
      if (!this._buffer) {
        const uri = this.data.assets && this.data.assets.music;
        if (!uri || !uri.includes(',')) throw new Error('内嵌音乐缺失');
        const encoded = uri.slice(uri.indexOf(',') + 1);
        const binary = root.atob(encoded);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
        this._buffer = await new Promise((resolve, reject) => {
          // Callback form also supports older mobile Safari versions.
          const promise = context.decodeAudioData(bytes.buffer, resolve, reject);
          if (promise && typeof promise.then === 'function') promise.then(resolve, reject);
        });
      }
      if (context.state !== 'running') await context.resume();
      if (context.state !== 'running') throw new Error('音频尚未启动，请再次轻点开始。');
    }
    async start(practice) {
      if (this.loading) return false;
      const token = ++this._token;
      this._stopSources(); this.state = 'ready'; this.loading = true; this.error = ''; this._emit();
      try {
        await this._ensureAudio();
        if (token !== this._token) return false;
        this._buildJudge(!!practice); this._resetActors(); this.judgement = null;
        this._nextStage = 0; this._readyPlayed = false; this._time = 0; this._segmentOffset = 0;
        this._songStart = this._context.currentTime + .04 + 4 * this.beatDuration;
        this.state = 'countin';
        this._scheduleSource(this._songStart, 0);
        this._scheduleCountIn(4 * this.beatDuration);
        this.loading = false; this._emit();
        return true;
      } catch (error) {
        if (token !== this._token) return false;
        this.loading = false; this._stopSources(); this.state = 'error';
        this.error = error && error.message ? error.message : '音乐解码失败，请使用 Safari 或 Chrome 重新打开文件。';
        this._emit(); return false;
      }
    }
    _scheduleSource(when, offset) {
      const source = this._context.createBufferSource();
      source.buffer = this._buffer; source.connect(this._context.destination);
      this._source = source;
      source.onended = () => {
        if (source !== this._source || !this._isActive()) return;
        this.tick();
      };
      source.start(when, offset, Math.max(.001, this.duration - offset));
    }
    _scheduleCountIn(remaining) {
      const first = this._songStart - remaining;
      for (let i = 0; i < 4; i++) {
        const when = this._songStart - (4 - i) * this.beatDuration;
        if (when >= first - .001 && when >= this._context.currentTime) this._tone(i === 3 ? 1000 : 660, when, .045);
      }
    }
    _tone(frequency, when, duration) {
      const context = this._context;
      if (!context || context.state !== 'running' || !context.createOscillator) return;
      try {
        const oscillator = context.createOscillator(), gain = context.createGain();
        const start = when == null ? context.currentTime : when;
        const length = duration || .04;
        oscillator.type = 'sine'; oscillator.frequency.value = frequency;
        gain.gain.setValueAtTime(.045, start);
        gain.gain.exponentialRampToValueAtTime(.0001, start + length);
        oscillator.connect(gain); gain.connect(context.destination);
        oscillator.start(start); oscillator.stop(start + length);
        this._toneSources.push(oscillator);
        oscillator.onended = () => { this._toneSources = this._toneSources.filter(value => value !== oscillator); try { oscillator.disconnect(); gain.disconnect(); } catch (_) {} };
      } catch (_) { /* Feedback is optional; the music clock remains authoritative. */ }
    }
    _stopSources() {
      if (this._source) {
        this._source.onended = null;
        try { this._source.stop(); this._source.disconnect(); } catch (_) {}
        this._source = null;
      }
      for (const oscillator of this._toneSources) { oscillator.onended = null; try { oscillator.stop(); oscillator.disconnect(); } catch (_) {} }
      this._toneSources = [];
    }
    _updateClock() {
      if (!this._isActive() || !this._context) return;
      const now = this._context.currentTime;
      if (this.state === 'countin' && now >= this._songStart) this.state = 'playing';
      this._time = this.state === 'playing' ? clamp(this._segmentOffset + Math.max(0, now - this._songStart), 0, this.duration) : 0;
    }
    _sample() { return Math.round(this._time * this.sampleRate) - Math.round(this.offset * this.sampleRate / 1000); }
    tick(emit) {
      this._updateClock();
      if (this.state === 'playing') {
        this._advanceStage(); this.judge.advance(this._sample());
        if (this._time >= this.duration) this._finish();
      }
      this._animate();
      if (emit !== false) this._emit();
      return this;
    }
    _advanceStage() {
      const readyAt = this.beatDuration * 14;
      const sample = Math.round(this._time * this.sampleRate);
      const playReady = () => {
        this._readyPlayed = true;
        for (const actor of this.actors) {
          actor.nextLeft = false;
          actor.motion = 3 + (actor.phase ? 11 : 0);
          actor.startTime = readyAt; actor.duration = this.motionDurations[actor.motion] / 60;
        }
      };
      while (this._nextStage < this.notes.length && this.notes[this._nextStage].sample <= sample) {
        const note = this.notes[this._nextStage++];
        // Retain event order even if a single render frame spans several early cues.
        if (!this._readyPlayed && note.time >= readyAt) playReady();
        this.captain.input = note.input; this.captain.startTime = note.time; this.captain.beatCueCount++;
        for (const actor of this.actors) if (!actor.isPlayer) this._playActor(actor, note.action, note.phaseBefore, note.nextLeftBefore, note.time);
      }
      // The only unscored preparation event (beat 14) applies to every seal.
      if (!this._readyPlayed && sample >= Math.round(readyAt * this.sampleRate)) playReady();
    }
    _playActor(actor, action, phase, nextLeft, time) {
      actor.startPhase = !!phase; actor.phase = !!phase; actor.nextLeft = !!nextLeft;
      if (action === NOD) actor.motion = phase ? 12 : 1;
      else if (action === ROLL) {
        actor.motion = (nextLeft ? 9 : 8) + (phase ? 0 : 11);
        actor.phase = !phase; actor.nextLeft = !nextLeft;
      } else {
        actor.motion = (nextLeft ? 4 : 5) + (phase ? 11 : 0);
        actor.nextLeft = !nextLeft;
      }
      actor.upside = actor.phase;
      actor.startTime = time; actor.duration = this.motionDurations[actor.motion] / 60;
      actor.frame = 0;
    }
    _visualTime() { return this.state === 'ready' || this.state === 'error' ? this._clock() : this._time; }
    _animate() {
      const time = this._visualTime();
      for (const actor of this.actors) {
        actor.frame = actor.startTime < 0 ? this.motionDurations[actor.motion] : clamp((time - actor.startTime) * 60, 0, this.motionDurations[actor.motion]);
      }
      const elapsed = this._time - this.captain.startTime;
      this.captain.frame = Math.max(0, elapsed * 60);
      this.captain.pose = this.captain.startTime >= 0 && elapsed < .2 ? this.captain.input === NOD ? 3 : this.captain.input === LEFT ? 1 : 2 : 0;
    }
    press(input) {
      return this._press(input, false);
    }
    pressBeat() {
      return this._press(NOD, true);
    }
    _press(input, automatic) {
      input = Number(input);
      if (![NOD,LEFT,RIGHT].includes(input) || this.loading) return null;
      this.tick(false);
      const player = this.actors[2];
      if (this.state === 'ready') {
        this._playActor(player, input, player.phase, input === NOD ? player.nextLeft : input === LEFT, this._visualTime());
        this._animate(); this._emit(); return null;
      }
      if (this.state !== 'playing') return null;
      const result = automatic ? this.judge.submitBeat(this._sample()) : this.judge.submit(input, this._sample());
      input = result.input;
      const note = result.noteIndex >= 0 ? this.notes[result.noteIndex] : null;
      if (result.grade === 'Perfect' || result.grade === 'Good') this._playActor(player, note.action, note.phaseBefore, note.nextLeftBefore, this._time);
      else {
        const action = input === NOD ? NOD : note && note.action === ROLL ? ROLL : input;
        this._playActor(player, action, player.phase, input === NOD ? player.nextLeft : input === LEFT, this._time);
      }
      this._animate(); this._emit();
      return result;
    }
    _onJudged(result) {
      const success = result.grade === 'Perfect' || result.grade === 'Good';
      this.judgement = {label:result.grade === 'Stray' ? '按多了' : result.grade.toUpperCase(), at:this._time, success, grade:result.grade, input:result.input, noteIndex:result.noteIndex, offsetMs:result.offsetMs};
      if (success) this._tone(result.grade === 'Perfect' ? 880 : 700);
    }
    pause() {
      if (!this._isActive()) return false;
      this._updateClock();
      if (this.state === 'playing' && this._time >= this.duration) { this._advanceStage(); this._finish(); this._animate(); this._emit(); return false; }
      if (this.state === 'playing') { this._advanceStage(); this.judge.advance(this._sample()); }
      this._pausedState = this.state;
      this._remainingCountIn = this.state === 'countin' ? Math.max(0, this._songStart - this._context.currentTime) : 0;
      this._stopSources(); this.state = 'paused'; this._animate(); this._emit();
      return true;
    }
    async resume() {
      if (this.state !== 'paused' || this.loading) return false;
      const token = ++this._token;
      this.loading = true; this.error = ''; this._emit();
      try {
        await this._ensureAudio();
        if (token !== this._token || this.state !== 'paused') return false;
        const now = this._context.currentTime + .04;
        this._segmentOffset = this._time;
        this._songStart = now + (this._pausedState === 'countin' ? this._remainingCountIn : 0);
        this.state = this._pausedState;
        this._scheduleSource(this._songStart, this._time);
        if (this.state === 'countin') this._scheduleCountIn(this._remainingCountIn);
        this.loading = false; this._emit(); return true;
      } catch (error) {
        if (token !== this._token) return false;
        this._stopSources(); this.state = 'paused'; this.loading = false;
        this.error = error && error.message ? error.message : '请再次轻点继续以启用音频。';
        this._emit(); return false;
      }
    }
    togglePause() { return this.state === 'paused' ? this.resume() : Promise.resolve(this.pause()); }
    restart() { return this.start(this.practice); }
    back() {
      ++this._token; this.loading = false; this._stopSources();
      this._time = 0; this._nextStage = 0; this._readyPlayed = false; this.judgement = null;
      this._resetActors(); this._buildJudge(false); this.state = 'ready'; this.error = ''; this._emit();
    }
    toggleAssist() { this.assist = !this.assist; this._save('seal_h5_assist', this.assist ? 1 : 0); this._emit(); return this.assist; }
    setOffset(milliseconds) { this.offset = clamp(Math.round(Number(milliseconds) || 0), -250, 250); this._save('seal_h5_offset', this.offset); this._emit(); return this.offset; }
    _finish() {
      this.judge.advance(Number.MAX_SAFE_INTEGER); this._stopSources(); this._time = this.duration; this.state = 'results';
      if (!this.practice && this.judge.score > this.best) { this.best = this.judge.score; this._save(BEST_KEY, this.best); }
    }
    _makeDialogue() {
      const cues = [], beat = this.beatDuration, countWords = ['','一','两','三','四','五','六','七','八','九','十'];
      for (const note of this._allNotes) {
        const position = note.time / beat;
        if (Math.abs(position - Math.floor(position) - .5) < .01) cues.push({start:note.time - .75 * beat, end:note.time + .75 * beat, text:'注意，半拍连击！'});
      }
      for (let i = 0; i < this._allNotes.length; i++) {
        if (this._allNotes[i].action !== ROLL) continue;
        const first = i;
        while (i + 1 < this._allNotes.length && this._allNotes[i + 1].action === ROLL && this._allNotes[i + 1].time - this._allNotes[i].time <= beat * 1.05) i++;
        const count = i - first + 1;
        cues.push({start:this._allNotes[first].time - 2 * beat, end:this._allNotes[i].time + .5 * beat, text:count === 1 ? '准备，翻滚一次！' : '连续翻滚，' + (countWords[count] || count) + '次！'});
      }
      return cues;
    }
    snapshot() {
      return {state:this.state, time:this.time, duration:this.duration, countdown:this.countdown, total:this.total, score:this.judge.score, perfect:this.judge.perfect, good:this.judge.good, miss:this.judge.miss, stray:this.judge.stray, combo:this.judge.combo, resolved:this.judge.resolvedCount, captain:{...this.captain}, actors:this.actors.map(actor => ({...actor})), dialogue:this.dialogue};
    }
    destroy() {
      ++this._token; this._stopSources(); this._listeners.clear();
      if (typeof document !== 'undefined') document.removeEventListener('visibilitychange', this._visibilityHandler);
      if (this._context) { this._context.onstatechange = null; try { const promise = this._context.close(); if (promise && promise.catch) promise.catch(() => {}); } catch (_) {} }
    }
  }
  root.SealGame = SealGame;
  root.SealJudge = SealJudge;
  if (typeof module !== 'undefined' && module.exports) module.exports = {SealGame, SealJudge};
})(typeof globalThis !== 'undefined' ? globalThis : window);
