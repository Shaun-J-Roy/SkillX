// State Management
let currentUser = null;
let currentProfiles = [];
let currentViewedProfileId = null;

// ================= UI & NAVIGATION =================
function showSection(id, btn) {
    document.querySelectorAll(".section").forEach(s => {
        s.classList.remove("active-section");
    });
    document.getElementById(id).classList.add("active-section");

    if (btn) {
        document.querySelectorAll(".menu-btn").forEach(b => b.classList.remove("active"));
        btn.classList.add("active");
    }
}

function switchAuthTab(tab) {
    document.querySelectorAll('.tab-btn').forEach(btn => btn.classList.remove('active'));
    document.querySelectorAll('.auth-section').forEach(sec => sec.classList.remove('active'));

    if (tab === 'login') {
        document.querySelectorAll('.tab-btn')[0].classList.add('active');
        document.getElementById('loginSection').classList.add('active');
    } else {
        document.querySelectorAll('.tab-btn')[1].classList.add('active');
        document.getElementById('registerSection').classList.add('active');
    }
}

function showToast(message, type = 'info') {
    const container = document.getElementById('toastContainer');
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;

    let icon = 'ℹ️';
    if (type === 'success') icon = '✅';
    if (type === 'error') icon = '⚠️';

    toast.innerHTML = `<span style="font-size:20px">${icon}</span> <span>${message}</span>`;
    container.appendChild(toast);

    setTimeout(() => {
        toast.classList.add('fade-out');
        setTimeout(() => toast.remove(), 400);
    }, 4000);
}

// ================= AUTHENTICATION =================
function registerUser() {
    const idValue = parseInt(document.getElementById("regId").value);
    const nameValue = document.getElementById("regName").value;
    const areaValue = document.getElementById("regArea").value;

    if (!idValue || !nameValue || !areaValue) {
        showToast("Please fill all registration fields.", "error");
        return;
    }

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({
            action: "registerUser",
            userId: idValue,
            name: nameValue,
            area: areaValue
        });
    } else {
        showToast("WebView connection not available.", "error");
    }
}

function loginUser() {
    const idValue = parseInt(document.getElementById("loginId").value);

    if (!idValue) {
        showToast("Please enter a valid User ID.", "error");
        return;
    }

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({
            action: "loginUser",
            userId: idValue
        });
    }
}

function logout() {
    currentUser = null;
    document.getElementById('authOverlay').style.display = 'flex';
    document.getElementById('mainApp').style.display = 'none';
    document.getElementById("loginId").value = "";
    showToast("Logged out successfully.", "info");
}

function setUserSession(user) {
    currentUser = user;
    document.getElementById('authOverlay').style.display = 'none';
    document.getElementById('mainApp').style.display = 'flex';

    // Update sidebar UI
    document.getElementById('navAvatar').innerText = user.Name.charAt(0).toUpperCase();
    document.getElementById('navUserName').innerText = user.Name;
    updateCreditsDisplay(user.Credits);

    // Default load
    loadGigs();
}

function updateCreditsDisplay(amount) {
    if (currentUser) {
        currentUser.Credits = amount;
        document.getElementById('navUserCredits').innerText = ` ${amount} Credits`;
    }
}

// ================= ACTIONS =================
function addSkill() {
    if (!currentUser) return;

    const title = document.getElementById('skillTitle').value;
    const category = document.getElementById('skillCategory').value;
    const desc = document.getElementById('skillDesc').value;
    const credits = parseInt(document.getElementById('skillCredits').value);

    if (!title || !category || !credits) {
        showToast("Please fill all skill fields.", "error");
        return;
    }

    window.chrome.webview.postMessage({
        action: "addSkill",
        userId: currentUser.UserID,
        title: title,
        category: category,
        description: desc,
        credits: credits
    });
}

function postGig() {
    if (!currentUser) return;

    const category = document.getElementById('gigCategory').value;
    const desc = document.getElementById('gigDescription').value;
    const credits = parseInt(document.getElementById('gigCredits').value);

    if (!category || !desc || !credits) {
        showToast("Please fill all gig fields.", "error");
        return;
    }

    window.chrome.webview.postMessage({
        action: "postGig",
        userId: currentUser.UserID,
        category: category,
        description: desc,
        credits: credits
    });
}

function loadGigs() {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ action: "getGigs" });
    }
}

function acceptGig(gigId, postedBy, credits) {
    if (!currentUser) return;

    window.chrome.webview.postMessage({
        action: "completeGig",
        gigId: gigId,
        postedBy: postedBy,
        worker: currentUser.UserID,
        credits: credits
    });
}

function loadProfiles() {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ action: "getProfiles" });
    }
}

function viewProfile(uid, name, area, credits, el = null) {
    currentViewedProfileId = uid;
    // UI selection state
    document.querySelectorAll('.interactive-list li').forEach(li => li.classList.remove('selected'));
    if (el) {
        el.classList.add('selected');
    } else if (typeof event !== 'undefined' && event.currentTarget) {
        event.currentTarget.classList.add('selected');
    }

    document.getElementById('profileEmptyState').style.display = 'none';
    document.getElementById('profileDetails').style.display = 'block';

    document.getElementById('detailAvatar').innerText = name.charAt(0).toUpperCase();
    document.getElementById('profileName').innerText = name;
    document.getElementById('profileArea').innerText = ` ${area}`;
    document.getElementById('profileCredits').innerText = ` ${credits} Credits`;

    window.chrome.webview.postMessage({
        action: "getProfileDetails",
        userId: uid
    });
}

function filterProfiles() {
    const term = document.getElementById('profileSearch').value.toLowerCase();
    const list = document.getElementById('profileList');
    list.innerHTML = "";

    const filtered = currentProfiles.filter(u => u.Name.toLowerCase().includes(term) || u.Area.toLowerCase().includes(term));

    filtered.forEach(u => {
        list.innerHTML += `
            <li onclick="viewProfile(${u.UserID}, '${u.Name.replace(/'/g, "\\'")}', '${u.Area.replace(/'/g, "\\'")}', ${u.Credits}, this)">
                <div class="user-avatar" style="width:32px;height:32px;font-size:14px">${u.Name.charAt(0).toUpperCase()}</div>
                <div>
                    <div style="font-weight:500">${u.Name}</div>
                    <div style="font-size:12px;color:var(--text-secondary)">${u.Area}</div>
                </div>
            </li>`;
    });
}

function hireUser(sellerId, credits, title) {
    if (!currentUser) return;

    if (confirm(`Hire for ${credits} credits? This will deduct from your balance.`)) {
        window.chrome.webview.postMessage({
            action: "hireSkill",
            buyerId: currentUser.UserID,
            sellerId: sellerId,
            skillTitle: title,
            credits: credits
        });
    }
}

function loadChart() {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ action: "getChart" });
    }
}

// ================= MESSAGE HANDLER =================
let chartInstance = null;

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', event => {
        const data = event.data;

        // General Success/Error handling
        if (data.type === "success") {
            showToast(data.message, "success");
            // Refresh views if necessary
            loadGigs();

            // Deduct local credits optimistically if posting a gig
            // Reset forms
            document.getElementById('skillTitle').value = '';
            document.getElementById('skillCategory').value = '';
            document.getElementById('skillDesc').value = '';
            document.getElementById('skillCredits').value = '';

            document.getElementById('gigCategory').value = '';
            document.getElementById('gigDescription').value = '';
            document.getElementById('gigCredits').value = '';

            // If they completed a gig, fetch new credits
            if (data.message.includes("completed") || data.message.includes("posted")) {
                if (currentUser && window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage({ action: "loginUser", userId: currentUser.UserID });
                }
            }
        }

        if (data.type === "error") {
            showToast(data.message, "error");
        }

        if (data.type === "loginSuccess") {
            if (document.getElementById('authOverlay').style.display !== 'none') {
                showToast(data.message, "success");
            }
            setUserSession(data.user);
        }

        if (data.type === "hireSuccess") {
            showToast(data.message, "success");
            loadProfiles(); // Refresh the list to see updated credits
            if (currentUser && window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({ action: "loginUser", userId: currentUser.UserID });
            }
        }

        // Render Gigs
        if (data.type === "gigs") {
            const list = document.getElementById("gigList");
            list.innerHTML = "";

            if (data.gigs.length === 0) {
                list.innerHTML = `<div class="empty-state" style="grid-column: 1/-1"><span class="icon-large">🌵</span><p>No gigs available right now.</p></div>`;
                return;
            }

            data.gigs.forEach(g => {
                const isOwnGig = currentUser && g.PostedBy === currentUser.UserID;

                list.innerHTML += `
                    <div class="gig-card">
                        <div class="card-header">
                            <span class="category-tag">Gig</span>
                            <span class="price-tag"> ${g.CreditsOffered}</span>
                        </div>
                        <h3>${g.Description}</h3>
                        <p>A student needs help with this task around campus.</p>
                        <div class="card-footer">
                            <span class="poster-info">Posted by ID: ${g.PostedBy}</span>
                            ${!isOwnGig ? `<button class="btn success" onclick="acceptGig(${g.GigID}, ${g.PostedBy}, ${g.CreditsOffered})">Accept</button>` : '<span class="poster-info">Your Gig</span>'}
                        </div>
                    </div>`;
            });
        }

        // Render Student Directory
        if (data.type === "profiles") {
            currentProfiles = data.users;
            filterProfiles(); // Initial populate
        }

        // Render Profile Details & Skills
        if (data.type === "profileDetails") {
            const skillsList = document.getElementById('profileSkills');
            skillsList.innerHTML = "";

            if (data.skills.length === 0) {
                skillsList.innerHTML = `<p style="color:var(--text-secondary)">No skills listed yet.</p>`;
                return;
            }

            data.skills.forEach(s => {
                // We don't have user ID context directly here easily if we didn't pass it back, 
                // but we know which profile we are viewing based on the DOM state.
                // However, fetching to get the current profile id from DOM is clunky.
                // Let's grab it from the title: Name (ID:X) -> wait, we removed ID from title.
                // We will rely on UI state or not hiring from this view immediately if it's oneself.

                skillsList.innerHTML += `
                    <div class="skill-card">
                        <div class="card-header">
                            <span class="category-tag">${s.Category}</span>
                            <span class="price-tag"> ${s.Credits}</span>
                        </div>
                        <h3>${s.Title}</h3>
                        <div class="card-footer" style="justify-content:flex-end; margin-top:20px;">
                            ${currentUser && currentUser.UserID === currentViewedProfileId ? '' : `<button class="btn primary" onclick="hireUser(${currentViewedProfileId}, ${s.Credits}, '${s.Title.replace(/'/g, "\\'")}')" id="hireBtnPlaceholder_${s.Title.replace(/\s/g, '')}">Hire Student</button>`}
                        </div>
                    </div>`;
            });
        }

        // Render Chart
        if (data.type === "chart") {
            if (chartInstance) chartInstance.destroy();

            const ctx = document.getElementById("creditChart");

            Chart.defaults.color = "#94a3b8";
            Chart.defaults.font.family = "'Inter', sans-serif";

            chartInstance = new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: data.labels,
                    datasets: [{
                        label: "Total Credits",
                        data: data.values,
                        backgroundColor: "rgba(220, 38, 38, 0.8)",
                        borderRadius: 6,
                        borderWidth: 0
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        y: {
                            grid: { color: "rgba(255,255,255,0.05)" },
                            beginAtZero: true
                        },
                        x: {
                            grid: { display: false }
                        }
                    }
                }
            });
        }
    });
}

// Init
window.onload = function () {
    // Show auth overlay on load
    document.getElementById('authOverlay').style.display = 'flex';
};