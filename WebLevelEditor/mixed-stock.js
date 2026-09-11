function stockWords(level) {
  const generated = new Set(level.categories.filter(c => c.transformsOnComplete)
    .map(c => c.transformResultCategoryId + ':' + c.transformResult.text.toLowerCase()));
  return level.categories.flatMap(c => c.words.filter(w => !generated.has(c.id + ':' + w.text.toLowerCase()))
    .map(word => ({categoryId:c.id, word})));
}

// Verify every possible category-completion order, not just one solution.
// Fewer incomplete categories than available holders means partial placements
// cannot occupy all holders and strand every complete set outside them.
function isMixedStockSafe(level, stock) {
  const n = level.categories.length;
  if (!n || n > 20) return false;
  const expected = stockWords(level).map(e => e.categoryId + ':' + e.word.text.toLowerCase()).sort();
  const actual = stock.map(e => e.categoryId + ':' + e.word.text.toLowerCase()).sort();
  if (JSON.stringify(expected) !== JSON.stringify(actual)) return false;
  const ids = level.categories.map(c => c.id), memo = new Map();
  const order = stock.map(e => ids.indexOf(e.categoryId));
  let visits = 0;
  function visit(mask, kept, count, transforms) {
    if (count === n) return true;
    const key = mask + ':' + kept;
    if (memo.has(key)) return memo.get(key);
    if (++visits > 20000) return false;
    const available = Array(n).fill(0);
    const cursor = Math.min(order.length,16 + count*4-transforms);
    for (let i=0;i<cursor;i++) available[order[i]]++;
    level.categories.forEach((c,i) => {
      if ((mask & (1<<i)) && c.transformsOnComplete) available[ids.indexOf(c.transformResultCategoryId)]++;
    });
    let incomplete = 0; const ready = [];
    for(let i=0;i<n;i++) if (!(mask & (1<<i))) {
      if (available[i] === 4) ready.push(i);
      else if (available[i] > 0) incomplete++;
    }
    if (!ready.length || incomplete >= 4-kept) { memo.set(key,false); return false; }
    for(const i of ready) {
      const transform = !!level.categories[i].transformsOnComplete;
      const keepFinal = !transform && cursor >= stock.length && n-count-1 <= 4-kept-1;
      if (!visit(mask | (1<<i), kept + (keepFinal ? 1 : 0),count+1,transforms+(transform?1:0))) {
        memo.set(key,false); return false;
      }
    }
    memo.set(key,true); return true;
  }
  return visit(0,0,0,0);
}

function mixScore(stock) {
  let score = 0;
  for(let i=16;i<stock.length;i++) {
    if (stock[i].categoryId !== stock[i-1].categoryId) score += 2;
    if(i>=18 && new Set(stock.slice(i-2,i+1).map(e=>e.categoryId)).size>1) score++;
  }
  return score;
}

function generateMixedStock(level) {
  const words = stockWords(level);
  let stock = null;
  for(let attempt=0;attempt<100 && !stock;attempt++) {
    const pending = [...level.categories], done = new Set(), categoryOrder=[];
    while(pending.length) {
      const ready = pending.filter(c => level.categories.filter(s => s.transformsOnComplete && s.transformResultCategoryId===c.id).every(s=>done.has(s.id)));
      if(!ready.length) throw new Error('Transformation cycle.');
      const c = ready[attempt===0?0:Math.floor(Math.random()*ready.length)];
      pending.splice(pending.indexOf(c),1); done.add(c.id); categoryOrder.push(c.id);
    }
    const candidate = categoryOrder.flatMap(id=>words.filter(e=>e.categoryId===id));
    if(isMixedStockSafe(level,candidate)) stock=candidate;
  }
  if(!stock) throw new Error('No verified mixed stock found for this level. Adjust the category graph.');
  let score=mixScore(stock);
  for(let attempt=0;attempt<160;attempt++) {
    const a=Math.floor(Math.random()*stock.length), b=Math.floor(Math.random()*stock.length);
    if(a===b || stock[a].categoryId===stock[b].categoryId) continue;
    [stock[a],stock[b]]=[stock[b],stock[a]];
    const next=mixScore(stock);
    if(next>=score && isMixedStockSafe(level,stock)) score=next;
    else [stock[a],stock[b]]=[stock[b],stock[a]];
  }
  return stock;
}
if(typeof module!=='undefined') module.exports={stockWords,isMixedStockSafe,generateMixedStock,mixScore};
