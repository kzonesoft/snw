let updateInterval;

async function updateData() {
    try {
        const response = await fetch('/api/wks/hwusage');
        const data = await response.json();
        const totalClients = data.length;
        document.getElementById('total-clients').textContent = totalClients;

        const tbody = document.getElementById('data-body');
        tbody.innerHTML = '';

        data.forEach(item => {
            const setColorClass = value => value >= 50 ? 'status-red' : 'status-green';
            const setColorLan = value => value < 1000 ? 'status-red' : 'status-green';
            const row = `
                <tr>
                    <td class="status-blue">${item.wksName || 'N/A'}</td>
                    <td class="${setColorClass(item.cpuLoad)}">${item.cpuLoad || 0}%</td>
                    <td class="${setColorClass(item.gpuLoad)}">${item.gpuLoad || 0}%</td>
                    <td class="${setColorClass(item.cpuTemp)}">${item.cpuTemp || 0}°C</td>
                    <td class="${setColorClass(item.gpuTemp)}">${item.gpuTemp || 0}°C</td>
                    <td>${item.cpuClock || 0} MHz</td>
                    <td>${item.gpuClock || 0} MHz</td>
                    <td>${item.cpuPow || 0} W</td>
                    <td>${item.gpuPow || 0} W</td>
                    <td>${item.cpuFan || 0} Rpm</td>
                    <td>${item.gpuFan || 0} Rpm</td>
                    <td>${item.ramUsage || 0}%</td>
                    <td>${item.ramSpeed || 0} MHz</td>
                    <td>${item.uploadSpeed || 0} Kbps</td>
                    <td>${item.downloadSpeed || 0} Kbps</td>
                    <td class="${setColorLan(item.lanSpeed)}">${item.lanSpeed || 0} Mbps</td>
                    <td>${item.ping || '0'} ms</td>
                    <td>${item.appRunning || 'N/A'}</td>
                    <td>${item.uptime || 'N/A'}</td>
                </tr>
            `;
            tbody.innerHTML += row;
        });
    } catch (error) {
        console.error('Error fetching data:', error);
    }
}

function startAutoUpdate() {
    updateData();
    updateInterval = setInterval(updateData, 5000);
}

function stopAutoUpdate() {
    clearInterval(updateInterval);
}
