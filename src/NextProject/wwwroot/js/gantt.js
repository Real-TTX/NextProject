/* The schedule of the projects: a project opens to show its todos. The rows are drawn by the server (the todos hidden); this only shows and hides them, and
   keeps aria-expanded true. Without the script the link "Alle Todos aufklappen" does the same. */
(function () {
  'use strict';

  document.addEventListener('click', function (event) {
    var button = event.target.closest && event.target.closest('[data-np-toggle]');
    if (!button) { return; }
    var key = button.getAttribute('data-np-toggle');
    var open = button.getAttribute('aria-expanded') !== 'true';
    button.setAttribute('aria-expanded', open ? 'true' : 'false');
    Array.prototype.forEach.call(document.querySelectorAll('[data-np-parent="' + key + '"]'), function (row) { row.hidden = !open; });
  });
})();
