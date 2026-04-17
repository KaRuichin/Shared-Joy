// ── Shared Joy — 访客端 SPA 逻辑 ──

(function () {
    "use strict";

    // ── 状态 ──
    let authToken = null;
    let guestId = null;
    let pollTimer = null;
    let searchDebounce = null;
    let consecutiveErrors = 0;          // 连续轮询失败次数
    var isOffline = false;               // 当前是否处于离线状态

    // ── DOM 引用 ──
    const authView = document.getElementById("auth-view");
    const mainView = document.getElementById("main-view");
    const pinInput = document.getElementById("pin-input");
    const pinSubmit = document.getElementById("pin-submit");
    const authError = document.getElementById("auth-error");
    const offlineBanner = document.getElementById("offline-banner");
    const toastContainer = document.getElementById("toast-container");

    const searchInput = document.getElementById("search-input");
    const searchResults = document.getElementById("search-results");

    const npEmpty = document.getElementById("np-empty");
    const npContent = document.getElementById("np-content");
    const npAlbumArt = document.getElementById("np-album-art");
    const npTrack = document.getElementById("np-track");
    const npArtist = document.getElementById("np-artist");
    const npProgress = document.getElementById("np-progress");
    const npTime = document.getElementById("np-time");

    const voteQueue = document.getElementById("vote-queue");

    // ── API 辅助 ──
    function api(method, path, body) {
        const opts = {
            method: method,
            headers: {}
        };
        if (authToken) {
            opts.headers["Authorization"] = "Bearer " + authToken;
        }
        if (body !== undefined) {
            opts.headers["Content-Type"] = "application/json";
            opts.body = JSON.stringify(body);
        }
        return fetch(path, opts).then(function (res) {
            setOnline();   // 收到任何响应都说明服务器可达
            if (res.status === 401) {
                // 令牌失效，返回认证界面
                showAuth();
                return null;
            }
            if (!res.ok) return null;
            return res.json();
        }).catch(function (err) {
            // fetch 失败（网络断开或服务器不可达）
            handleNetworkError();
            return null;
        });
    }

    // ── 视图切换 ──
    function showAuth() {
        authToken = null;
        guestId = null;
        stopPolling();
        // 重置离线状态（回到认证界面时清除横幅）
        isOffline = false;
        consecutiveErrors = 0;
        offlineBanner.classList.remove("visible");
        document.body.classList.remove("offline-mode");
        authView.classList.add("active");
        mainView.classList.remove("active");
        pinInput.value = "";
        pinInput.focus();
    }

    function showMain() {
        authView.classList.remove("active");
        mainView.classList.add("active");
        searchInput.value = "";
        searchResults.innerHTML = "";
        startPolling();
    }

    // ── PIN 认证 ──
    pinSubmit.addEventListener("click", submitPin);
    pinInput.addEventListener("keydown", function (e) {
        if (e.key === "Enter") submitPin();
    });

    function submitPin() {
        var pin = pinInput.value.trim();
        if (!pin) return;

        authError.classList.add("hidden");
        pinSubmit.disabled = true;
        pinSubmit.textContent = "Joining...";

        api("POST", "/api/auth", { pin: pin }).then(function (data) {
            pinSubmit.disabled = false;
            pinSubmit.textContent = "Join";

            if (!data || !data.success) {
                authError.textContent = (data && data.error) || "Failed to join";
                authError.classList.remove("hidden");
                return;
            }

            authToken = data.token;
            guestId = data.guestId;
            showMain();
        });
    }

    // ── 搜索（防抖 300ms）──
    searchInput.addEventListener("input", function () {
        clearTimeout(searchDebounce);
        var q = searchInput.value.trim();
        if (!q) {
            searchResults.innerHTML = "";
            return;
        }
        searchDebounce = setTimeout(function () { doSearch(q); }, 300);
    });

    function doSearch(q) {
        api("GET", "/api/search?q=" + encodeURIComponent(q)).then(function (data) {
            if (!data || !data.tracks) {
                searchResults.innerHTML = "";
                return;
            }
            renderSearchResults(data.tracks);
        });
    }

    function renderSearchResults(tracks) {
        if (!tracks.length) {
            searchResults.innerHTML = '<p style="color:var(--text-muted);padding:12px 0;text-align:center;">No results</p>';
            return;
        }

        var html = "";
        tracks.forEach(function (t) {
            html += '<div class="track-item">';
            if (t.albumImageUrl) {
                html += '<img class="track-art" src="' + escHtml(t.albumImageUrl) + '" alt="">';
            }
            html += '<div class="track-info">';
            html += '<p class="track-name">' + escHtml(t.name) + '</p>';
            html += '<p class="track-artist">' + escHtml(t.artists) + '</p>';
            html += '</div>';
            html += '<div class="track-action">';
            html += '<button class="btn btn-vote" data-track=\'' + escAttr(JSON.stringify(t)) + '\'>Vote</button>';
            html += '</div></div>';
        });
        searchResults.innerHTML = html;

        // 绑定投票按钮
        searchResults.querySelectorAll(".btn-vote").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var track = JSON.parse(btn.getAttribute("data-track"));
                doVote(track, btn);
            });
        });
    }

    // ── 投票 ──
    function doVote(track, btn) {
        btn.disabled = true;
        api("POST", "/api/vote", { guestId: guestId, track: track }).then(function (data) {
            btn.disabled = false;
            if (data && data.success) {
                btn.textContent = "Voted";
                btn.classList.add("voted", "vote-anim");
                setTimeout(function () { btn.classList.remove("vote-anim"); }, 300);
                refreshQueue();
            } else {
                btn.textContent = (data && data.error) || "Error";
                setTimeout(function () { btn.textContent = "Vote"; }, 1500);
            }
        });
    }

    function doUnvote(trackId, btn) {
        btn.disabled = true;
        api("DELETE", "/api/vote?trackId=" + encodeURIComponent(trackId) + "&guestId=" + encodeURIComponent(guestId))
            .then(function (data) {
                btn.disabled = false;
                if (data && data.success) {
                    btn.textContent = "Vote";
                    btn.classList.remove("voted");
                    btn.classList.add("vote-anim");
                    setTimeout(function () { btn.classList.remove("vote-anim"); }, 300);
                    refreshQueue();
                }
            });
    }

    // ── 投票队列 ──
    function refreshQueue() {
        api("GET", "/api/queue").then(function (data) {
            if (!data || !data.items) return;
            renderQueue(data.items);
        });
    }

    function renderQueue(items) {
        if (!items.length) {
            voteQueue.innerHTML = '<p class="empty-hint">No votes yet. Search and vote for a song!</p>';
            return;
        }

        var html = "";
        items.forEach(function (item, idx) {
            var t = item.track;
            var myVote = item.voterIds && item.voterIds.indexOf(guestId) !== -1;
            html += '<div class="track-item">';
            if (t.albumImageUrl) {
                html += '<img class="track-art" src="' + escHtml(t.albumImageUrl) + '" alt="">';
            }
            html += '<div class="track-info">';
            html += '<p class="track-name">' + escHtml(t.name) + '</p>';
            html += '<p class="track-artist">' + escHtml(t.artists) + '</p>';
            html += '<p class="vote-count">' + item.voteCount + (item.voteCount === 1 ? " vote" : " votes") + '</p>';
            html += '</div>';
            html += '<div class="track-action">';
            html += '<button class="btn btn-vote' + (myVote ? " voted" : "") + '" data-track-id="' + escAttr(t.id) + '" data-track=\'' + escAttr(JSON.stringify(t)) + '\'>';
            html += myVote ? "Voted" : "Vote";
            html += '</button>';
            html += '</div></div>';
        });
        voteQueue.innerHTML = html;

        // 绑定投票/取消投票按钮
        voteQueue.querySelectorAll(".btn-vote").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var trackId = btn.getAttribute("data-track-id");
                if (btn.classList.contains("voted")) {
                    doUnvote(trackId, btn);
                } else {
                    var track = JSON.parse(btn.getAttribute("data-track"));
                    doVote(track, btn);
                }
            });
        });
    }

    // ── Now Playing ──
    function refreshNowPlaying() {
        api("GET", "/api/now-playing").then(function (data) {
            if (!data || !data.track) {
                npEmpty.classList.remove("hidden");
                npContent.classList.add("hidden");
                return;
            }

            npEmpty.classList.add("hidden");
            npContent.classList.remove("hidden");

            npTrack.textContent = data.track.name || "";
            npArtist.textContent = data.track.artists || "";
            if (data.track.albumImageUrl) {
                npAlbumArt.src = data.track.albumImageUrl;
            }

            var dur = data.track.durationMs || 1;
            var prog = data.progressMs || 0;
            npProgress.style.width = Math.min((prog / dur) * 100, 100) + "%";
            npTime.textContent = formatMs(prog) + " / " + formatMs(dur);
        });
    }

    // ── 轮询（每 3 秒）──
    function startPolling() {
        stopPolling();
        refreshQueue();
        refreshNowPlaying();
        pollTimer = setInterval(function () {
            refreshQueue();
            refreshNowPlaying();
        }, 3000);
    }

    function stopPolling() {
        if (pollTimer) {
            clearInterval(pollTimer);
            pollTimer = null;
        }
    }

    // ── 离线检测 ──
    function handleNetworkError() {
        consecutiveErrors++;
        // 连续 2 次失败才显示离线横幅，避免单次抖动误报
        if (consecutiveErrors >= 2 && !isOffline) {
            isOffline = true;
            offlineBanner.classList.add("visible");
            document.body.classList.add("offline-mode");
            showToast("Connection lost. Retrying…");
        }
    }

    function setOnline() {
        if (isOffline) {
            isOffline = false;
            offlineBanner.classList.remove("visible");
            document.body.classList.remove("offline-mode");
            showToast("Connection restored.");
        }
        consecutiveErrors = 0;
    }

    // ── Toast 工具 ──
    function showToast(message, durationMs) {
        durationMs = durationMs || 2500;
        var el = document.createElement("div");
        el.className = "toast-msg";
        el.textContent = message;
        toastContainer.appendChild(el);

        // 触发动画（rAF 确保浏览器已完成初次布局）
        requestAnimationFrame(function () {
            requestAnimationFrame(function () { el.classList.add("show"); });
        });

        setTimeout(function () {
            el.classList.remove("show");
            setTimeout(function () {
                if (el.parentNode) el.parentNode.removeChild(el);
            }, 300);
        }, durationMs);
    }

    // ── 辅助函数 ──
    function formatMs(ms) {
        var totalSec = Math.floor(ms / 1000);
        var min = Math.floor(totalSec / 60);
        var sec = totalSec % 60;
        return min + ":" + (sec < 10 ? "0" : "") + sec;
    }

    function escHtml(str) {
        if (!str) return "";
        return str.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
    }

    function escAttr(str) {
        if (!str) return "";
        return str.replace(/&/g, "&amp;").replace(/'/g, "&#39;").replace(/"/g, "&quot;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    }

})();
