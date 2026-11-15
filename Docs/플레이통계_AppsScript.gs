/**
 * Mob_archive 플레이 통계 수신 스크립트 (구글 Apps Script 웹 앱)
 *
 * 게임(RunAnalytics.cs)이 보낸 기록을 검사한 뒤 이 스프레드시트의 runs / errors 탭에 한 줄씩 쌓습니다.
 * 탭이 없으면 처음 기록이 들어올 때 머리글과 함께 자동으로 만듭니다.
 *
 * [안전 장치]
 * - TOKEN이 게임 설정(PlayStatsSettings의 Token)과 같은 기록만 받습니다.
 * - 정해진 열만 받고, 글자 수를 자르고, ID류 열은 영문/숫자/한글/일부 기호 외의 글자를 지웁니다.
 * - =, +, -, @ 로 시작하는 글자는 앞에 ' 를 붙여 수식으로 실행되지 않게 합니다(시트 수식 주입 방지).
 * - 하루에 받는 줄 수에 상한(DAILY_LIMIT)을 둡니다(누가 주소를 알아내 기록을 퍼부어도 시트가 무한히 커지지 않게).
 * - 게임에는 짧은 글자(ok 등)만 돌려주며, 시트 내용은 절대 돌려주지 않습니다.
 */

const TOKEN = 'mob-7c85583b9352d66b67de0f71';
const DAILY_LIMIT = 5000;
const MAX_BODY = 30000;

const RUN_COLUMNS = [
  'recordId', 'version', 'editor', 'playerId', 'startType', 'endType', 'reroll', 'playTime',
  'levelReached', 'bossReached', 'roomsEntered',
  'deathLevel', 'deathRoom', 'deathRoomVariant', 'deathRoomOrder', 'deathRoomOrderInLevel',
  'deathAttacker', 'deathAttack', 'deathEnemy',
  'codexCount', 'codexTotal', 'codexIds', 'acquired', 'appeared', 'held',
  'heldT0', 'heldT1', 'heldT2', 'heldT3', 'enemiesKilled', 'roomsCleared'
];

const ERROR_COLUMNS = ['recordId', 'version', 'editor', 'playerId', 'runId', 'level', 'scene', 'message', 'stack'];

// 자유로운 문장이 들어가는 열(에러 내용). 나머지 글자 열은 ID용 글자만 남깁니다.
const FREE_TEXT_COLUMNS = ['message', 'stack'];

function doPost(e) {
  try {
    if (!e || !e.postData || typeof e.postData.contents !== 'string') return reply('bad');
    const raw = e.postData.contents;
    if (raw.length === 0 || raw.length > MAX_BODY) return reply('bad');

    let data;
    try { data = JSON.parse(raw); } catch (err) { return reply('bad'); }
    if (!data || typeof data !== 'object' || data.k !== TOKEN) return reply('denied');

    let sheetName, columns;
    if (data.type === 'run') { sheetName = 'runs'; columns = RUN_COLUMNS; }
    else if (data.type === 'error') { sheetName = 'errors'; columns = ERROR_COLUMNS; }
    else return reply('bad');

    const lock = LockService.getScriptLock();
    if (!lock.tryLock(10000)) return reply('busy');
    try {
      if (!underDailyLimit()) return reply('limit');
      const sheet = ensureSheet(sheetName, columns);
      const row = [new Date()].concat(columns.map(function (c) { return clean(c, data[c]); }));
      sheet.appendRow(row);
    } finally {
      lock.releaseLock();
    }
    return reply('ok');
  } catch (err) {
    return reply('error');
  }
}

// 브라우저로 주소를 열었을 때(GET) - 아무 정보도 보여주지 않습니다.
function doGet() {
  return reply('ok');
}

function reply(text) {
  return ContentService.createTextOutput(text).setMimeType(ContentService.MimeType.TEXT);
}

function clean(column, value) {
  if (value === undefined || value === null) return '';
  if (typeof value === 'number') return isFinite(value) ? value : '';
  if (typeof value === 'boolean') return value ? 1 : 0;

  let s = String(value);
  if (FREE_TEXT_COLUMNS.indexOf(column) >= 0) {
    s = s.replace(/[\r\n\t]+/g, ' ').slice(0, 1000);
  } else {
    s = s.replace(/[^A-Za-z0-9_.,:\- \uAC00-\uD7A3]/g, '').slice(0, 3000);
  }
  if (/^[=+\-@]/.test(s)) s = "'" + s;
  return s;
}

function ensureSheet(name, columns) {
  const book = SpreadsheetApp.getActiveSpreadsheet();
  let sheet = book.getSheetByName(name);
  if (!sheet) {
    sheet = book.insertSheet(name);
    sheet.appendRow(['receivedAt'].concat(columns));
    sheet.setFrozenRows(1);
  }
  return sheet;
}

function underDailyLimit() {
  const props = PropertiesService.getScriptProperties();
  const today = Utilities.formatDate(new Date(), 'Asia/Seoul', 'yyyy-MM-dd');
  const key = 'count_' + today;
  const count = Number(props.getProperty(key) || '0');
  if (count >= DAILY_LIMIT) return false;
  props.setProperty(key, String(count + 1));
  if (props.getProperty('last_day') !== today) {
    const old = props.getProperty('last_day');
    if (old) props.deleteProperty('count_' + old);
    props.setProperty('last_day', today);
  }
  return true;
}
