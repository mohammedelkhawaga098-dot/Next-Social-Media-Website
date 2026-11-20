(() => {
  'use strict';

  const $ = (sel, root = document) => root.querySelector(sel);
  const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));
  const csrf = () => $('meta[name="csrf"]')?.content ?? '';
  const meId = document.body.dataset.userId || '';

  function el(tag, cls, text) {
    const n = document.createElement(tag);
    if (cls) n.className = cls;
    if (text != null) n.textContent = text;
    return n;
  }

  // ---------- Network ----------
  async function request(url, { method = 'GET', data, json = false } = {}) {
    const init = { method, headers: { 'X-Requested-With': 'fetch' } };
    if (method !== 'GET') {
      init.headers['RequestVerificationToken'] = csrf();
      if (data instanceof FormData) init.body = data;
      else if (data) { const fd = new FormData(); Object.entries(data).forEach(([k, v]) => fd.append(k, v ?? '')); init.body = fd; }
    }
    const res = await fetch(url, init);
    if (!res.ok) {
      let msg = 'Something went wrong. Please try again.';
      try { const j = await res.json(); if (j?.error) msg = j.error; } catch { /* not json */ }
      if (res.status === 401 || res.redirected) location.reload();
      throw new Error(msg);
    }
    return json ? res.json() : res.text();
  }
  const postJson = (url, data) => request(url, { method: 'POST', data, json: true });
  const postHtml = (url, data) => request(url, { method: 'POST', data });

  // ---------- Toasts ----------
  function toast(message, type = 'info', url = null) {
    const host = $('#toasts');
    if (!host) return;
    const t = el(url ? 'a' : 'div', `toast ${type}`, message);
    if (url) t.href = url;
    host.appendChild(t);
    setTimeout(() => t.remove(), 5000);
  }
  const flash = $('#toasts');
  if (flash?.dataset.toast) toast(flash.dataset.toast, flash.dataset.toastType || 'ok');

  // ---------- Dialogs ----------
  function dialog({ title, message = '', input = null, confirmText = 'Confirm', danger = false }) {
    return new Promise(resolve => {
      const overlay = el('div', 'modal-overlay');
      const box = el('div', 'modal-box');
      box.append(el('h2', null, title));
      if (message) box.append(el('p', null, message));
      let field = null;
      if (input) {
        field = el('textarea', 'textarea');
        field.placeholder = input;
        field.maxLength = 2000;
        box.append(field);
      }
      const actions = el('div', 'modal-actions');
      const cancel = el('button', 'n-btn ghost', 'Cancel');
      const ok = el('button', `n-btn ${danger ? 'danger' : 'primary'}`, confirmText);
      const close = value => { overlay.remove(); document.removeEventListener('keydown', onKey); resolve(value); };
      const onKey = e => { if (e.key === 'Escape') close(null); };
      cancel.onclick = () => close(null);
      ok.onclick = () => close(field ? field.value : true);
      overlay.onclick = e => { if (e.target === overlay) close(null); };
      document.addEventListener('keydown', onKey);
      actions.append(cancel, ok);
      box.append(actions);
      overlay.append(box);
      document.body.append(overlay);
      (field || ok).focus();
    });
  }

  document.addEventListener('submit', async e => {
    const form = e.target.closest('form[data-confirm]');
    if (!form || form.dataset.confirmed) return;
    e.preventDefault();
    const ok = await dialog({ title: 'Are you sure?', message: form.dataset.confirm, confirmText: 'Yes, continue', danger: true });
    if (ok) { form.dataset.confirmed = '1'; form.requestSubmit(); }
  });

  // ---------- Time ----------
  const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  function formatTime(date, mode) {
    const now = new Date();
    const hm = date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    if (mode === 'time') {
      return date.toDateString() === now.toDateString() ? hm : `${MONTHS[date.getMonth()]} ${date.getDate()}, ${hm}`;
    }
    const s = Math.floor((now - date) / 1000);
    if (s < 45) return 'just now';
    if (s < 3600) return `${Math.max(1, Math.round(s / 60))}m`;
    if (s < 86400) return `${Math.round(s / 3600)}h`;
    if (s < 7 * 86400) return `${Math.round(s / 86400)}d`;
    const y = date.getFullYear() === now.getFullYear() ? '' : `, ${date.getFullYear()}`;
    return `${MONTHS[date.getMonth()]} ${date.getDate()}${y}`;
  }
  function localizeTimes(root = document) {
    $$('time[datetime]', root).forEach(t => {
      const d = new Date(t.getAttribute('datetime'));
      if (isNaN(d)) return;
      t.textContent = formatTime(d, t.dataset.t || 'ago');
      t.title = d.toLocaleString();
    });
  }
  localizeTimes();
  setInterval(() => localizeTimes(), 60000);

  // ---------- Badges & presence ----------
  function setBadge(key, count) {
    $$(`[data-badge="${key}"]`).forEach(b => { b.textContent = count; b.hidden = count <= 0; });
  }
  function bumpBadge(key, by = 1) {
    const first = $(`[data-badge="${key}"]`);
    setBadge(key, (parseInt(first?.textContent, 10) || 0) + by);
  }
  function setPresence(userId, online) {
    $$(`[data-presence="${userId}"]`).forEach(n => n.classList.toggle('on', online));
    $$(`[data-presence-text="${userId}"]`).forEach(n => { n.textContent = online ? 'Online' : 'Offline'; });
  }

  // ---------- Feed ----------
  async function loadFeed(box, url) {
    try {
      box.innerHTML = await request(url);
      localizeTimes(box);
    } catch (err) {
      box.innerHTML = '';
      box.append(el('div', 'card empty', err.message));
    }
  }
  const feed = $('#feed');
  if (feed) loadFeed(feed, feed.dataset.feedUrl);

  // ---------- Composer ----------
  const composer = $('[data-composer]');
  if (composer) {
    const text = $('textarea', composer), file = $('input[type=file]', composer);
    const preview = $('.composer-preview', composer), img = $('img', preview), submit = $('[data-submit]', composer);
    const autosize = () => { text.style.height = 'auto'; text.style.height = Math.min(text.scrollHeight, 240) + 'px'; };
    text.addEventListener('input', autosize);
    file.addEventListener('change', () => {
      const f = file.files[0];
      if (!f) return;
      img.src = URL.createObjectURL(f);
      preview.hidden = false;
    });
    $('[data-clear-image]', composer).onclick = () => { file.value = ''; preview.hidden = true; };
    submit.addEventListener('click', async () => {
      if (!text.value.trim() && !file.files[0]) { toast('Write something or add a photo.', 'error'); return; }
      submit.disabled = true;
      try {
        const fd = new FormData();
        fd.append('content', text.value);
        if (file.files[0]) fd.append('image', file.files[0]);
        const html = await postHtml('/Posts/Create', fd);
        const list = $('#feed');
        list?.querySelector('.empty')?.closest('.card')?.remove();
        list?.insertAdjacentHTML('afterbegin', html);
        localizeTimes(list);
        text.value = ''; file.value = ''; preview.hidden = true; autosize();
      } catch (err) { toast(err.message, 'error'); }
      finally { submit.disabled = false; }
    });
  }

  // ---------- Post interactions ----------
  const bump = (card, sel, delta) => { const n = $(sel, card); if (n) n.textContent = Math.max(0, (+n.textContent || 0) + delta); };

  document.addEventListener('click', async e => {
    const more = e.target.closest('[data-load-more]');
    if (more) {
      more.disabled = true;
      try {
        const url = new URL(feed.dataset.feedUrl, location.origin);
        url.searchParams.set('page', more.dataset.nextPage);
        const html = await request(url.pathname + url.search);
        more.closest('.load-more').remove();
        feed.insertAdjacentHTML('beforeend', html);
        localizeTimes(feed);
      } catch (err) { toast(err.message, 'error'); more.disabled = false; }
      return;
    }

    const btn = e.target.closest('[data-act]');
    if (!btn) return;
    const card = btn.closest('.post');
    const id = card?.dataset.postId;

    try {
      switch (btn.dataset.act) {
        case 'like': {
          const r = await postJson(`/Posts/Like/${id}`);
          btn.classList.toggle('liked', r.liked);
          $('i', btn).className = `${r.liked ? 'fa-solid' : 'fa-regular'} fa-heart`;
          $('[data-like-count]', card).textContent = r.count;
          break;
        }
        case 'comments': {
          const box = $('.comments', card);
          if (!box.dataset.loaded) {
            box.innerHTML = await request(`/Posts/Comments/${id}`);
            box.dataset.loaded = '1';
            localizeTimes(box);
          }
          box.hidden = !box.hidden;
          if (!box.hidden) $('input', box)?.focus();
          break;
        }
        case 'delete-comment': {
          if (!await dialog({ title: 'Delete comment?', message: 'This can\'t be undone.', confirmText: 'Delete', danger: true })) return;
          const c = btn.closest('.comment');
          const r = await postJson(`/Posts/DeleteComment/${c.dataset.commentId}`);
          c.remove();
          $('[data-comment-count]', card).textContent = r.count;
          break;
        }
        case 'share': {
          const msg = await dialog({ title: 'Share this post', message: 'It will appear on your profile.', input: 'Say something about it (optional)', confirmText: 'Share' });
          if (msg === null) return;
          await postJson(`/Posts/Share/${id}`, { content: msg });
          bump(card, '[data-share-count]', 1);
          toast('Shared to your profile.', 'ok');
          break;
        }
        case 'delete': {
          if (!await dialog({ title: 'Delete post?', message: 'This can\'t be undone.', confirmText: 'Delete', danger: true })) return;
          await postJson(`/Posts/Delete/${id}`);
          card.remove();
          toast('Post deleted.', 'ok');
          break;
        }
        case 'edit': startEdit(card, id); break;
      }
    } catch (err) { toast(err.message, 'error'); }
  });

  function startEdit(card, id) {
    if ($('.post-edit', card)) return;
    const textEl = $('[data-text]', card);
    const wrap = el('div', 'post-edit');
    const area = el('textarea', 'textarea');
    area.maxLength = 2000;
    area.value = textEl ? textEl.textContent : '';
    const actions = el('div', 'modal-actions');
    const cancel = el('button', 'n-btn ghost', 'Cancel');
    const save = el('button', 'n-btn primary', 'Save');
    actions.append(cancel, save);
    wrap.append(area, actions);
    const body = $('.post-body', card);
    if (textEl) textEl.hidden = true;
    body.prepend(wrap);
    area.focus();
    cancel.onclick = () => { wrap.remove(); if (textEl) textEl.hidden = false; };
    save.onclick = async () => {
      save.disabled = true;
      try {
        const html = await postHtml(`/Posts/Edit/${id}`, { content: area.value });
        card.outerHTML = html;
        localizeTimes();
      } catch (err) { toast(err.message, 'error'); save.disabled = false; }
    };
  }

  document.addEventListener('submit', async e => {
    const form = e.target.closest('.comment-form');
    if (!form) return;
    e.preventDefault();
    const input = $('input', form);
    if (!input.value.trim()) return;
    const card = form.closest('.post');
    try {
      const html = await postHtml(`/Posts/AddComment/${card.dataset.postId}`, { content: input.value });
      const list = $('.comment-list', card);
      $('[data-empty]', card)?.remove();
      list.insertAdjacentHTML('beforeend', html);
      localizeTimes(list);
      bump(card, '[data-comment-count]', 1);
      input.value = '';
    } catch (err) { toast(err.message, 'error'); }
  });

  // ---------- Messages page ----------
  const thread = $('.thread');
  const chatPage = $('.chat');
  const activeId = thread?.dataset.recipientId || null;

  function addBubble(m, mine) {
    const row = el('div', `msg ${mine ? 'mine' : 'theirs'}`);
    const bubble = el('div', 'bubble');
    if (m.imagePath) {
      const a = el('a'); a.href = m.imagePath; a.target = '_blank'; a.rel = 'noopener';
      const im = el('img'); im.src = m.imagePath; im.alt = 'Attachment'; im.loading = 'lazy';
      a.append(im); bubble.append(a);
    }
    if (m.content) bubble.append(el('div', 'msg-text', m.content));
    const t = el('time'); t.dataset.t = 'time'; t.setAttribute('datetime', new Date(m.sentAt).toISOString());
    bubble.append(t); row.append(bubble); thread.append(row);
    localizeTimes(row);
    thread.scrollTop = thread.scrollHeight;
  }

  if (thread) {
    thread.scrollTop = thread.scrollHeight;
    const form = $('#chat-form');
    if (form) {
      const area = $('textarea', form), file = $('input[type=file]', form), name = $('.attach-name');
      const send = async () => {
        if (!area.value.trim() && !file.files[0]) return;
        const fd = new FormData();
        fd.append('recipientId', activeId);
        fd.append('text', area.value);
        if (file.files[0]) fd.append('image', file.files[0]);
        $('button[type=submit]', form).disabled = true;
        try {
          const m = await request('/Messages/Send', { method: 'POST', data: fd, json: true });
          addBubble(m, true);
          area.value = ''; file.value = ''; name.hidden = true;
          touchConversation(activeId, m.content || 'Photo', false);
        } catch (err) { toast(err.message, 'error'); }
        finally { $('button[type=submit]', form).disabled = false; area.focus(); }
      };
      form.addEventListener('submit', e => { e.preventDefault(); send(); });
      area.addEventListener('keydown', e => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); send(); } });
      file.addEventListener('change', () => { name.textContent = file.files[0] ? `Attached: ${file.files[0].name}` : ''; name.hidden = !file.files[0]; });
    }
  }

  function touchConversation(userId, preview, unread) {
    const item = $(`.convo[data-user-id="${userId}"]`);
    if (!item) return;
    $('.preview', item).textContent = preview;
    const when = $('time', item);
    if (when) { when.setAttribute('datetime', new Date().toISOString()); localizeTimes(item); }
    if (unread) {
      item.classList.add('unread');
      let b = $('.nav-badge', item);
      if (!b) { b = el('span', 'nav-badge', '0'); item.append(b); }
      b.textContent = (parseInt(b.textContent, 10) || 0) + 1;
    }
    const h = item.parentElement.querySelector('h2');
    if (h) h.after(item); // newest conversation goes to the top
  }

  // ---------- SignalR ----------
  if (meId && window.signalR) {
    const conn = new signalR.HubConnectionBuilder().withUrl('/hubs/next').withAutomaticReconnect().build();

    conn.on('PresenceChanged', (userId, online) => setPresence(userId, online));

    conn.on('ReceiveNotification', n => {
      const inOpenThread = n.type === 'Message' && activeId && n.url.endsWith(`with=${$('.chat-head [data-username]')?.dataset.username}`);
      if (inOpenThread) { request('/Messages/MarkRead', { method: 'POST', data: { userId: activeId } }).catch(() => {}); return; }
      setBadge('notifications', n.unreadCount);
      toast(n.text, 'info', n.url);
    });

    conn.on('ReceiveMessage', m => {
      if (activeId && m.senderId === activeId && thread) {
        addBubble(m, false);
        request('/Messages/MarkRead', { method: 'POST', data: { userId: activeId } }).catch(() => {});
        touchConversation(m.senderId, m.content || 'Photo', false);
      } else {
        bumpBadge('messages');
        touchConversation(m.senderId, m.content || 'Photo', true);
      }
    });

    conn.start().catch(err => console.warn('Real-time connection failed', err));
  }

  window.NEXT = { toast, dialog, localizeTimes };
})();
