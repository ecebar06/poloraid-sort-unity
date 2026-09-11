const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {isMixedStockSafe,generateMixedStock,mixScore} = require('./mixed-stock');
const dir = path.join(__dirname,'../Assets/_Game/Resources/Data/Levels');
for(const file of fs.readdirSync(dir).filter(f=>f.endsWith('.json'))) {
  const level=JSON.parse(fs.readFileSync(path.join(dir,file),'utf8'));
  assert.ok(isMixedStockSafe(level,level.orderedWords),file);
  assert.ok(!isMixedStockSafe(level,level.orderedWords.slice(1)),'Missing word rejected');
  for(let i=0;i<10;i++) assert.ok(isMixedStockSafe(level,generateMixedStock(level)));
  console.log(file+': saved queue + 10 generated queues verified, mix score '+mixScore(level.orderedWords));
}
const bad={categories:Array.from({length:5},(_,i)=>({id:'c'+i,words:Array.from({length:4},(_,j)=>({text:i+'-'+j})),transformsOnComplete:false}))};
const partial=bad.categories.flatMap(c=>c.words.slice(0,3).map(word=>({categoryId:c.id,word})));
const tail=bad.categories.map(c=>({categoryId:c.id,word:c.words[3]}));
assert.equal(isMixedStockSafe(bad,[...partial,...tail]),false,'Four partial categories can lock holders');
console.log('Partial-holder deadlock rejected.');
