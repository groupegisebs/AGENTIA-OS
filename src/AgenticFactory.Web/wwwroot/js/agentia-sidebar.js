(function () {
    'use strict';

    var STORAGE_KEY = 'agentia-nav-sections';

    function readStoredState() {
        try {
            var raw = localStorage.getItem(STORAGE_KEY);
            return raw ? JSON.parse(raw) : {};
        } catch (_) {
            return {};
        }
    }

    function writeStoredState(state) {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
        } catch (_) { /* ignore quota / private mode */ }
    }

    function setExpanded(group, expanded) {
        group.classList.toggle('is-collapsed', !expanded);
        var toggle = group.querySelector('.af-nav-section-toggle');
        if (toggle) {
            toggle.setAttribute('aria-expanded', expanded ? 'true' : 'false');
        }
    }

    function initSidebarSections() {
        var nav = document.querySelector('.af-nav');
        if (!nav) return;

        var groups = nav.querySelectorAll('.af-nav-section-group');
        if (!groups.length) return;

        var stored = readStoredState();

        groups.forEach(function (group) {
            var sectionId = group.getAttribute('data-section');
            if (!sectionId) return;

            var expanded = stored[sectionId] !== false;
            setExpanded(group, expanded);

            var toggle = group.querySelector('.af-nav-section-toggle');
            if (!toggle) return;

            toggle.addEventListener('click', function () {
                var isCollapsed = group.classList.contains('is-collapsed');
                var nextExpanded = isCollapsed;
                setExpanded(group, nextExpanded);

                stored = readStoredState();
                stored[sectionId] = nextExpanded;
                writeStoredState(stored);
            });
        });

        var activeLink = nav.querySelector('.af-nav-item a.active');
        if (activeLink) {
            var activeGroup = activeLink.closest('.af-nav-section-group');
            if (activeGroup) {
                var activeId = activeGroup.getAttribute('data-section');
                setExpanded(activeGroup, true);
                if (activeId) {
                    stored = readStoredState();
                    stored[activeId] = true;
                    writeStoredState(stored);
                }
            }
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initSidebarSections);
    } else {
        initSidebarSections();
    }
})();
