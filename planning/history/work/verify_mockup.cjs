// Offline rendering of strings only: no browser, filesystem access from the page, or network.
const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const html = fs.readFileSync('outputs/업무정리_목업_작업기록확대.html', 'utf8');
const script = html.split('<script>')[1].split('</script>')[0];
new vm.Script(script);
const listeners = {}, classes = {};
const app = {innerHTML:''};
const document = {
  activeElement:null,
  getElementById(id){return id==='app'?app:null;},
  querySelector(){return null;},
  addEventListener(name,fn){listeners[name]=fn;},
  body:{classList:{toggle(name,value){classes[name]=value;}}}
};
const sandbox = {document,window:{addEventListener(){},scrollTo(){}},getComputedStyle(){throw Error('No layout simulation');}};
vm.runInNewContext(script,sandbox,{timeout:1000});
const click = (act,arg)=>{
  const el = {tagName:'BUTTON',getAttribute(k){return k==='data-act'?act:arg;}};
  listeners.click({target:{closest(){return el;}}});
};
assert.match(app.innerHTML,/작업 기록 영역/);
assert.match(app.innerHTML,/aria-label="최대화"/);
const gutter=app.innerHTML.match(/<pre class="record-gutter"[^>]*>([^<]+)<\/pre>/)[1];
assert.equal(gutter.split('\n').length,60);
assert.match(app.innerHTML,/D:\\업무관리\\2026주기\\C업무_2026-03/);
click('toggleDetailMax');
assert.match(app.innerHTML,/win detail maximized/);
assert.equal(classes['detail-is-max'],true);
assert.match(app.innerHTML,/aria-label="원래 크기로"/);
listeners.keydown({key:'Escape'});
assert.doesNotMatch(app.innerHTML,/win detail maximized/);
click('toggleDetailMax');
click('overlay','past');
assert.match(app.innerHTML,/role="dialog" aria-label="지난 제출물"/);
listeners.keydown({key:'Escape'});
assert.match(app.innerHTML,/win detail maximized/);
listeners.keydown({key:'Escape'});
assert.doesNotMatch(app.innerHTML,/win detail maximized/);
click('screen','done');
assert.match(app.innerHTML,/aria-label="완료 작업 창"/);
assert.equal(classes['detail-is-max'],false);
click('selDone','C2');
assert.match(app.innerHTML,/C_1 결과물.hwp/);
click('copyList');
assert.match(app.innerHTML,/実際|실제 클립보드에는 복사하지 않았습니다/);
click('toMain');
click('closeDetail');
assert.doesNotMatch(app.innerHTML,/aria-label="작업 상세 창"/);
click('openTask','A');
assert.match(app.innerHTML,/서버 접속 시간 초과/);
assert.match(app.innerHTML,/aria-label="최대화"/);
assert.doesNotMatch(html,/<link[^>]*https?:/);
console.log('PASS: syntax, 60-line gutter, maximize/restore/Escape, past-submission overlay, completed screen, return/open, local font references.');
console.log('No browser layout or visible-line count has been measured.');
