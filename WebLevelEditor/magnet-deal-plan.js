// Continuous admission: one new category per completion, not a batch barrier.
function selectNextMagnets(level, done, active, looseCount, retained, limit = 1) {
  const incoming = id => level.categories.filter(c => c.transformsOnComplete && c.transformResultCategoryId === id);
  let occupied = looseCount;
  const selected = [];
  const ready = level.categories.filter(c => !done.has(c.id) && !active.has(c.id) && incoming(c.id).every(s => done.has(s.id)))
    .sort((a,b) => incoming(b.id).length - incoming(a.id).length);
  for (const c of ready) {
    const cost = 4 - incoming(c.id).length;
    if (selected.length >= limit || active.size + retained + selected.length >= 4 || occupied + cost > 16) continue;
    selected.push(c.id); occupied += cost;
  }
  return selected;
}

function buildMagnetDealPlan(level) {
  const done = new Set(), active = new Set(), waves = [];
  let loose = 0, retained = 0;
  const incoming = id => level.categories.filter(c => c.transformsOnComplete && c.transformResultCategoryId === id);
  while (done.size < level.categories.length) {
    const added = selectNextMagnets(level, done, active, loose, retained, done.size ? 1 : 4);
    if (added.length) waves.push(added);
    for (const id of added) { active.add(id); loose += 4 - incoming(id).length; }
    if (!active.size) throw new Error('Transformation graph cannot fit 16 loose magnets and four holders.');
    const id = level.categories.find(c => active.has(c.id)).id;
    const c = level.categories.find(c => c.id === id);
    active.delete(id); done.add(id); loose -= 4;
    if (c.transformsOnComplete) loose++;
    else if (level.categories.length - done.size < 4) retained++;
  }
  return waves;
}
if (typeof module !== 'undefined') module.exports = { buildMagnetDealPlan, selectNextMagnets };
