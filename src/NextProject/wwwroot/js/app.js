/* NextProject: the behaviour of the app itself. MatUi (wwwroot/ui/matui.js) does menus, dialogs, tabs, validation, pickers and the schedule; this file adds only what is
   specific to the app: the box that finishes a todo, the board, positions that are data. Everything is wired by data attributes, so server-rendered and dynamically added
   markup behave the same. No inline styles anywhere: a value that is data is set here, as a custom property (allowed by the content security policy). */
(function () {
  'use strict';

  var csrf = function () {
    var meta = document.querySelector('meta[name="np-csrf"]');
    return meta ? meta.getAttribute('content') : '';
  };
  var toast = function (message, tone) {
    if (window.MatUi && window.MatUi.toast) { window.MatUi.toast(message, { tone: tone || 'danger' }); }
  };

  // ---- positions that are data: <span data-np-bar="12.5,30"> sets --np-left and --np-width (percent) ---------------------------------------------------------
  function applyBars(scope) {
    Array.prototype.forEach.call((scope || document).querySelectorAll('[data-np-bar]'), function (el) {
      var parts = el.getAttribute('data-np-bar').split(',');
      var left = parseFloat(parts[0]);
      var width = parseFloat(parts[1]);
      if (!isNaN(left)) { el.style.setProperty('--np-left', left + '%'); }
      if (!isNaN(width) && width > 0) { el.style.setProperty('--np-width', width + '%'); }
    });
  }
  applyBars(document);

  // The line for today in the schedule sits on the track column, not on the label column: its left is measured from the track.
  function placeToday() {
    Array.prototype.forEach.call(document.querySelectorAll('.np-tl__rows'), function (rows) {
      var today = rows.querySelector('.np-tl__today');
      var track = rows.querySelector('.np-tl__track');
      if (!today || !track) { return; }
      var percent = parseFloat((today.getAttribute('data-np-bar') || '0').split(',')[0]);
      var base = track.getBoundingClientRect();
      var host = rows.getBoundingClientRect();
      if (base.width > 0) { today.style.setProperty('left', (base.left - host.left + base.width * percent / 100) + 'px'); }
    });
  }
  window.addEventListener('resize', placeToday);
  document.addEventListener('click', function (event) { if (event.target.closest && event.target.closest('[role="tab"]')) { setTimeout(placeToday, 0); } });
  placeToday();

  // ---- the box that finishes a todo -----------------------------------------------------------------------------------------------------------------------------
  function postStatus(id, to, extra) {
    var body = new URLSearchParams(Object.assign({ to: to }, extra || {}));
    return fetch('/api/todos/' + encodeURIComponent(id) + '/status', {
      method: 'POST',
      headers: { 'RequestVerificationToken': csrf(), 'Content-Type': 'application/x-www-form-urlencoded', 'X-Requested-With': 'fetch' },
      credentials: 'same-origin',
      body: body.toString()
    }).then(function (response) {
      return response.json().catch(function () { return {}; }).then(function (data) {
        if (!response.ok) { throw new Error(data.error || 'Das hat nicht geklappt.'); }
        return data;
      });
    });
  }

  document.addEventListener('change', function (event) {
    var box = event.target;
    if (!box || !box.matches || !box.matches('input[data-np-done]')) { return; }
    var wasChecked = box.checked;
    box.disabled = true;
    postStatus(box.getAttribute('data-np-done'), wasChecked ? 'Done' : 'Open').then(function (data) {
      var row = box.closest('tr, li');
      if (row) {
        var pill = row.querySelector('.mat-pill');
        if (pill) { pill.textContent = data.label; pill.className = 'mat-pill' + (wasChecked ? ' mat-pill--ok' : ''); }
        Array.prototype.forEach.call(row.querySelectorAll('.mat-table__name a, .mat-listgroup__text strong'), function (a) { a.classList.toggle('np-done', wasChecked); });
      }
      box.disabled = false;
      toast(wasChecked ? 'Erledigt. Gut gemacht.' : 'Das Todo ist wieder offen.', 'success');
    }).catch(function (error) {
      box.checked = !wasChecked;
      box.disabled = false;
      toast(error.message);
    });
  });

  // ---- the board: a card dragged to another column changes its status --------------------------------------------------------------------------------------------
  var dragged = null;

  function updateCounts(board) {
    Array.prototype.forEach.call(board.querySelectorAll('[data-np-col]'), function (col) {
      var count = col.querySelector('[data-np-count]');
      if (count) { count.textContent = col.querySelectorAll('[data-np-card]').length; }
    });
  }

  document.addEventListener('dragstart', function (event) {
    var card = event.target.closest && event.target.closest('[data-np-card]');
    if (!card) { return; }
    dragged = card;
    card.classList.add('is-dragging');
    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/plain', card.getAttribute('data-np-card'));
  });
  document.addEventListener('dragend', function () {
    if (dragged) { dragged.classList.remove('is-dragging'); }
    dragged = null;
    Array.prototype.forEach.call(document.querySelectorAll('.np-board__col.is-over'), function (c) { c.classList.remove('is-over'); });
  });
  document.addEventListener('dragover', function (event) {
    var col = event.target.closest && event.target.closest('[data-np-col]');
    if (!col || !dragged) { return; }
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
    col.classList.add('is-over');
  });
  document.addEventListener('dragleave', function (event) {
    var col = event.target.closest && event.target.closest('[data-np-col]');
    if (col && !col.contains(event.relatedTarget)) { col.classList.remove('is-over'); }
  });
  document.addEventListener('drop', function (event) {
    var col = event.target.closest && event.target.closest('[data-np-col]');
    if (!col || !dragged) { return; }
    event.preventDefault();
    col.classList.remove('is-over');
    var card = dragged;
    var to = col.getAttribute('data-np-col');
    var from = card.getAttribute('data-np-status');
    if (to === from) { return; }
    var board = col.closest('[data-np-board]');
    var home = card.parentNode;
    var next = card.nextSibling;

    function send(extra) {
      card.classList.add('is-busy');
      return postStatus(card.getAttribute('data-np-card'), to, extra).then(function (data) {
        col.querySelector('[data-np-drop]').insertBefore(card, col.querySelector('[data-np-drop]').firstChild);
        card.setAttribute('data-np-status', to);
        var select = card.querySelector('select[name="to"]');
        if (select) { select.value = to; }
        card.classList.remove('is-busy');
        updateCounts(board);
        toast('Status: ' + data.label, 'success');
      }).catch(function (error) {
        card.classList.remove('is-busy');
        home.insertBefore(card, next);
        toast(error.message);
      });
    }

    if (to === 'Blocked') {
      // A block needs its reason: ask for it, the same question as on the page of the todo.
      if (window.MatUi && window.MatUi.prompt) {
        window.MatUi.prompt('Was blockiert die Aufgabe?', { title: 'Blockiert', required: true }).then(function (reason) {
          if (reason) { send({ reason: reason }); }
        });
      } else {
        toast('Zum Blockieren brauchst du einen Grund. Öffne das Todo.');
      }

      return;
    }

    send();
  });
})();

// A control that applies at once: <select data-submit-on-change> sends its form when it changes.
document.addEventListener('change', function (event) {
  var target = event.target;
  if (target && target.hasAttribute && target.hasAttribute('data-submit-on-change') && target.form) { target.form.submit(); }
});

// Scrollbars are only visible while something scrolls: the class is set on the scrolled element and removed a moment later.
(function () {
  'use strict';
  var timers = new WeakMap();
  document.addEventListener('scroll', function (event) {
    var el = event.target === document ? document.documentElement : event.target;
    if (!el || !el.classList) { return; }
    el.classList.add('is-scrolling');
    clearTimeout(timers.get(el));
    timers.set(el, setTimeout(function () { el.classList.remove('is-scrolling'); }, 900));
  }, true);
})();
