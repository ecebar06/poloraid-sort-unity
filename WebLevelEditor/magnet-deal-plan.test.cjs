const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {buildMagnetDealPlan, selectNextMagnets} = require('./magnet-deal-plan.js');
const dir = path.join(__dirname, '../Assets/_Game/Resources/Data/Levels');
const key = (id, text) => `${id}:${text.toLowerCase()}`;

function simulate(level) {
  buildMagnetDealPlan(level);
  const sources = level.categories.filter(c => c.transformsOnComplete);
  const generated = new Set(sources.map(c => key(c.transformResultCategoryId, c.transformResult.text)));
  const loose = new Set(), completed = new Set(), active = new Set(), kept = new Set();
  const queue = [], collected = new Map();
  let peak = 0, deals = 0, refilledWhileOthersActive = false;
  function refill(limit) {
    const added = selectNextMagnets(level, completed, active, loose.size, kept.size, limit);
    if (added.length && active.size) refilledWhileOthersActive = true;
    if (added.length) deals++;
    for (const id of added) {
      active.add(id);
      const c = level.categories.find(c => c.id === id);
      for (const w of c.words) {
        const k = key(id,w.text);
        if (!generated.has(k)) { assert.ok(!loose.has(k)); loose.add(k); }
        assert.ok(loose.has(k), 'Missing '+k);
        queue.push({c,k});
      }
    }
    assert.ok(active.size + kept.size <= 4);
    assert.ok(loose.size <= 16);
    peak = Math.max(peak, loose.size);
  }
  refill(4);
  while (completed.size < level.categories.length) {
    assert.ok(queue.length, 'Deadlock');
    const [{c,k}] = queue.splice(Math.floor(Math.random()*queue.length),1);
    assert.ok(loose.delete(k));
    collected.set(c.id,(collected.get(c.id)||0)+1);
    if (collected.get(c.id) === 4) {
      active.delete(c.id); completed.add(c.id);
      if (c.transformsOnComplete) {
        const output = key(c.transformResultCategoryId,c.transformResult.text);
        assert.ok(!loose.has(output)); loose.add(output);
      } else if (level.categories.length-completed.size < 4) kept.add(c.id);
      refill(1);
    }
    assert.ok(loose.size<=16);
    peak=Math.max(peak,loose.size);
  }
  assert.equal(loose.size,0);
  assert.ok(kept.size>0 && kept.size<=4);
  return {waves:deals,peak,refilledWhileOthersActive};
}

for (const file of fs.readdirSync(dir).filter(f => f.endsWith('.json'))) {
  const level = JSON.parse(fs.readFileSync(path.join(dir, file), 'utf8').replace(/^\uFEFF/, ''));
  let result;
  for (let i = 0; i < 100; i++) result = simulate(level);
  console.log(`${file}: 100 random playthroughs passed; ${result.waves} refill events, peak ${result.peak}`);
}
const finals = {categories: Array.from({length: 20}, (_, i) => ({id: `c${i}`, words:
  Array.from({length:4}, (_, j) => ({text:`w${i}_${j}`})), transformsOnComplete:false}))};
assert.equal(simulate(finals).waves, 17);
assert.ok(simulate(finals).refilledWhileOthersActive, 'Must refill before other holders finish');
assert.throws(() => buildMagnetDealPlan({categories:[
  {id:'a', transformsOnComplete:true, transformResultCategoryId:'b'},
  {id:'b', transformsOnComplete:true, transformResultCategoryId:'a'}
]}));
console.log('20 reusable final holders and cycle rejection passed.');
