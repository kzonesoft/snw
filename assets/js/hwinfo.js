let updateInterval;

async function updateData() {
    try {
        const token = sessionStorage.getItem('token');
        if (!token) {
            window.location.href = '/login';
            stopAutoUpdate();
            return;
        }

        const response = await fetch('/api/wks/hwinfo', {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`,
            },
        });

        if (response.status === 401) {
            sessionStorage.removeItem('token');
            window.location.href = '/login';
            stopAutoUpdate();
            return;
        }

        const data = await response.json();
        const totalClients = data.length;
        document.getElementById('total-clients').textContent = totalClients;

        const tbody = document.getElementById('data-body');
        tbody.innerHTML = '';

        data.forEach(item => {
            const row = `
                <tr>
                    <td class="status-blue">${item.WksName || 'N/A'}</td>
                    <td>${item.CpuName || 'N/A'}</td>
                    <td>${item.GpuName || 'N/A'}</td>
                    <td>${item.MainboardName || 'N/A'}</td>
                    <td>${item.LanName || 'N/A'}</td>
                    <td>${item.RamTotal || '0'} GB</td>
                    <td>${item.Virtualization ? 'Có' : 'Không'}</td>
                    <td>${item.Mac || 'N/A'}</td>
                </tr>
            `;
            tbody.innerHTML += row;
        });
    } catch (error) {
        console.error('Error fetching hardware info:', error);
    }
}

function startAutoUpdate() {
    updateData();
    updateInterval = setInterval(updateData, 60000); // Cập nhật mỗi 1 phút
}

function stopAutoUpdate() {
    clearInterval(updateInterval);
}