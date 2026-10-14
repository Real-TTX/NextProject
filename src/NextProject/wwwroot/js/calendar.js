/* The calendar page: what MatUi's schedule does not do. A click on a todo opens a dialog (move, finish, open), a todo dragged to another day in the month view moves
   its due date, and a click on a day sets the day of "Neues Todo". The server decides what is allowed (/api/todos/{id}/due and /status); this file only asks and then draws
   the answer into the schedule again. No inline styles. */
(function () {
  'use strict';

  var root = document.getElementById('calendar');
  if (!root) { return; }

  var events = [];
  try { events = JSON.parse(root.getAttribute('data-events') || '[]'); } catch (e) { events = []; }
  var byId = {};
  events.forEach(function (e) { byId[e.id] = e; });

  var dialog = document.getElementById('cal-todo');
  var current = null;

  var csrf = function () {
    var meta = document.querySelector('meta[name="np-csrf"]');
    return meta ? meta.getAttribute('content') : '';
  };
  var toast = function (message, tone) { if (window.MatUi && window.MatUi.toast) { window.MatUi.toast(message, { tone: tone || 'danger' }); } };

  // ---- dates as text (yyyy-mm-dd) without a time zone ------------------------------------------------------------------------------------------------------------
  function toUtc(iso) { var p = iso.split('-'); return Date.UTC(+p[0], +p[1] - 1, +p[2]); }
  function fromUtc(ms) { var d = new Date(ms); return d.getUTCFullYear() + '-' + String(d.getUTCMonth() + 1).padStart(2, '0') + '-' + String(d.getUTCDate()).padStart(2, '0'); }
  function addDays(iso, days) { return fromUtc(toUtc(iso) + days * 86400000); }
  function daysBetween(a, b) { return Math.round((toUtc(b) - toUtc(a)) / 86400000); }
  function german(iso) { var p = iso.split('-'); return p[2] + '.' + p[1] + '.' + p[0]; }
  function dueOf(ev) { return ev.end || ev.start; }

  function redraw() {
    if (window.MatUi && window.MatUi.schedule) { window.MatUi.schedule.setEvents(root, events); }
  }

  function post(url, fields) {
    return fetch(url, {
      method: 'POST',
      headers: { 'RequestVerificationToken': csrf(), 'Content-Type': 'application/x-www-form-urlencoded', 'X-Requested-With': 'fetch' },
      credentials: 'same-origin',
      body: new URLSearchParams(fields).toString()
    }).then(function (response) {
      return response.json().catch(function () { return {}; }).then(function (data) {
        if (!response.ok) { throw new Error(data.error || 'Das hat nicht geklappt.'); }
        return data;
      });
    });
  }

  // The event the server sent back moved: the same todo with new dates. A start date moved by the same number of days, the length stays.
  function applyMove(ev, data) {
    var due = data.due, start = data.start;
    if (start && start < due) { ev.start = start; ev.end = due; } else { ev.start = due; delete ev.end; }
    if (ev.status && /überfällig/.test(ev.status)) { ev.status = ev.status.replace(/,? überfällig/, ''); if (ev.tone === 'error' && !/Blockiert/.test(ev.status)) { ev.tone = ''; } }
    redraw();
  }

  function move(ev, newDue) {
    if (newDue === dueOf(ev)) { return Promise.resolve(); }
    return post('/api/todos/' + encodeURIComponent(ev.entityId) + '/due', { due: newDue }).then(function (data) {
      applyMove(ev, data);
      toast('Verschoben auf ' + german(data.due) + '.', 'success');
    }).catch(function (error) { toast(error.message); });
  }

  // ---- the dialog of a todo --------------------------------------------------------------------------------------------------------------------------------
  function part(name) { return dialog.querySelector('[data-cal="' + name + '"]'); }

  function openTodo(ev) {
    current = ev;
    document.getElementById('cal-todo-title').textContent = ev.title;
    part('meta').textContent = [ev.status, ev.note].filter(Boolean).join(' · ');
    part('due').value = dueOf(ev);
    part('error').textContent = '';
    part('open').setAttribute('href', ev.url || '#');
    var closed = !ev.movable;
    part('move').disabled = closed;
    part('done').disabled = closed;
    part('due').disabled = closed;
    window.MatUi.dialog.open(dialog);
  }

  if (dialog) {
    part('move').addEventListener('click', function () {
      var value = part('due').value;
      if (!value) { part('error').textContent = 'Bitte wähle ein Datum.'; return; }
      move(current, value).then(function () { window.MatUi.dialog.close(dialog); });
    });
    part('done').addEventListener('click', function () {
      post('/api/todos/' + encodeURIComponent(current.entityId) + '/status', { to: 'Done' }).then(function (data) {
        current.tone = 'ok';
        current.status = data.label;
        current.movable = false;
        redraw();
        window.MatUi.dialog.close(dialog);
        toast('Erledigt. Gut gemacht.', 'success');
      }).catch(function (error) { part('error').textContent = error.message; });
    });
  }

  root.addEventListener('mat:schedule-select', function (event) {
    var raw = event.detail;
    if (!raw || raw.kind !== 'todo' || !dialog) { return; }
    event.preventDefault();
    openTodo(byId[raw.id] || raw);
  });

  // ---- a click on a day: the day of "Neues Todo" -----------------------------------------------------------------------------------------------------------
  var newTodo = document.getElementById('cal-new');
  var newDay = document.getElementById('cal-new-day');
  root.addEventListener('mat:schedule-day', function (event) {
    if (!newTodo) { return; }
    var day = event.detail.date;
    var base = newTodo.getAttribute('data-cal-new') || '/Todos/Edit';
    newTodo.setAttribute('href', base + (base.indexOf('?') >= 0 ? '&' : '?') + 'due=' + day);
    if (newDay) { newDay.textContent = 'am ' + german(day); }
  });

  // ---- a todo dragged to another day (the month view) -----------------------------------------------------------------------------------------------------
  var drag = null;

  function eventOf(node) {
    var host = node.closest && node.closest('[data-ev]');
    var schedule = root._matSchedule;
    if (!host || !schedule) { return null; }
    var inner = schedule.events[parseInt(host.getAttribute('data-ev'), 10)];
    return inner ? (byId[inner.raw.id] || inner.raw) : null;
  }
  function dayOf(node) {
    var cell = node.closest && node.closest('.mat-schedule__cell');
    var button = cell && cell.querySelector('[data-sch-day]');
    return button ? button.getAttribute('data-sch-day') : null;
  }

  // The schedule draws its markup again at every change; the chips of todos that may move are made draggable each time (a link is not always dragged on its own).
  function markDraggable() {
    Array.prototype.forEach.call(root.querySelectorAll('.mat-schedule__event--chip[data-ev]'), function (chip) {
      var ev = eventOf(chip);
      if (ev && ev.kind === 'todo' && ev.movable) { chip.setAttribute('draggable', 'true'); } else { chip.removeAttribute('draggable'); }
    });
  }
  new MutationObserver(markDraggable).observe(root, { childList: true, subtree: true });
  markDraggable();

  root.addEventListener('dragstart', function (event) {
    var ev = eventOf(event.target);
    if (!ev) { return; }
    if (ev.kind !== 'todo' || !ev.movable) { event.preventDefault(); return; }
    drag = { ev: ev, from: dayOf(event.target) || dueOf(ev) };
    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/plain', ev.title);
  });
  root.addEventListener('dragend', function () {
    drag = null;
    Array.prototype.forEach.call(root.querySelectorAll('.np-drop'), function (c) { c.classList.remove('np-drop'); });
  });
  root.addEventListener('dragover', function (event) {
    var cell = drag && event.target.closest && event.target.closest('.mat-schedule__cell');
    if (!cell || !dayOf(cell)) { return; }
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
    cell.classList.add('np-drop');
  });
  root.addEventListener('dragleave', function (event) {
    var cell = event.target.closest && event.target.closest('.mat-schedule__cell');
    if (cell && !cell.contains(event.relatedTarget)) { cell.classList.remove('np-drop'); }
  });
  root.addEventListener('drop', function (event) {
    var cell = drag && event.target.closest && event.target.closest('.mat-schedule__cell');
    var day = cell && dayOf(cell);
    if (!day) { return; }
    event.preventDefault();
    cell.classList.remove('np-drop');
    var ev = drag.ev;
    // moved by the number of days between the day it was grabbed on and the day it was dropped on
    var shift = daysBetween(drag.from, day);
    drag = null;
    move(ev, addDays(dueOf(ev), shift));
  });
})();
