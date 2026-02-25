function showSection(id) {
    document.querySelectorAll(".section").forEach(s => s.style.display = "none");
    document.getElementById(id).style.display = "block";
}

function registerUser() {

    const nameValue = document.getElementById("name").value;
    const areaValue = document.getElementById("area").value;

    if (!nameValue || !areaValue) {
        alert("Please fill all fields.");
        return;
    }

    window.chrome.webview.postMessage({
        action: "registerUser",
        name: nameValue,
        area: areaValue
    });

    alert("User Registered!");

    loadUsers();
}

function addSkill() {
    window.chrome.webview.postMessage({
        action: "addSkill",
        userId: parseInt(skillUser.value),
        title: skillTitle.value,
        category: skillCategory.value,
        credits: parseInt(skillCredits.value)
    });
    alert("Skill Added!");
}

function postGig() {
    window.chrome.webview.postMessage({
        action: "postGig",
        userId: parseInt(gigUser.value),
        category: gigCategory.value,
        description: gigDescription.value,
        credits: parseInt(gigCredits.value)
    });
    alert("Gig Posted!");
}

function loadUsers() {
    window.chrome.webview.postMessage({ action: "getUsers" });
}

function loadGigs() {
    window.chrome.webview.postMessage({ action: "getGigs" });
}

function acceptGig(gigId, postedBy, credits) {
    const workerId = prompt("Enter your User ID:");
    if (!workerId) return;

    window.chrome.webview.postMessage({
        action: "completeGig",
        gigId: gigId,
        postedBy: postedBy,
        worker: parseInt(workerId),
        credits: credits
    });

    alert("Gig Completed!");
    loadGigs();
    loadUsers();
}

function loadChart() {
    window.chrome.webview.postMessage({ action: "getChart" });
}

window.chrome.webview.addEventListener('message', event => {

    const data = event.data;

    if (data.type === "users") {
        skillUser.innerHTML = "";
        gigUser.innerHTML = "";

        data.users.forEach(u => {
            skillUser.innerHTML += `<option value="${u.UserID}">${u.Name}</option>`;
            gigUser.innerHTML += `<option value="${u.UserID}">${u.Name}</option>`;
        });
    }

    if (data.type === "gigs") {
        gigList.innerHTML = "";
        data.gigs.forEach(g => {
            gigList.innerHTML += `
                <li>
                    ${g.Description} - ${g.CreditsOffered} credits
                    <button onclick="acceptGig(${g.GigID}, ${g.PostedBy}, ${g.CreditsOffered})">
                        Accept
                    </button>
                </li>`;
        });
    }

    if (data.type === "chart") {
        new Chart(document.getElementById("creditChart"), {
            type: 'bar',
            data: {
                labels: data.labels,
                datasets: [{
                    label: "Credits",
                    data: data.values
                }]
            }
        });
    }

});

window.onload = function () {
    showSection("register");
    loadUsers();
};