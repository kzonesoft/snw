// Sử dụng IIFE (Immediately Invoked Function Expression) để tránh xung đột biến toàn cục
(function () {
    let updateInterval = null;
    let isUpdating = false;

    const wksModule = {
        updateData: async function () {
            if (!isUpdating) return;

            try {
                const token = sessionStorage.getItem('token'); // Lấy token từ localStorage
                if (!token) {
                    window.location.href = '/login'; // Điều hướng đến trang login
                    this.stopAutoUpdate();
                    return;
                }

                document.getElementById('total-clients').textContent = '...'; // Hiển thị đang tải

                const response = await fetch('/api/wks/hwusage', {
                    method: 'GET',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${token}`,
                    },
                });

                if (response.status === 401) {
                    // Nếu response là 401, xóa token và làm mới trang
                    sessionStorage.removeItem('token'); // Xóa token
                    window.location.href = '/login'; // Điều hướng đến trang login
                    this.stopAutoUpdate();
                    return; // Thoát khỏi hàm
                }

                const data = await response.json();
                const totalClients = data.length;
                document.getElementById('total-clients').textContent = totalClients;

                const tbody = document.getElementById('data-body');
                tbody.innerHTML = '';

                const setColor100 = value => {
                    if (value < 60) {
                        return 'status-green';
                    } else if (value >= 60 && value <= 84) {
                        return 'status-orange';
                    } else {
                        return 'status-red';
                    }
                };

                const setColor2500 = value => value < 1000 ? 'status-red' : 'status-green';

                data.forEach(item => {
                    const row = `
                        <tr>
                            <td class="status-blue">${item.wksName || 'N/A'}</td>
                            <td class="${setColor100(item.cpuLoad)}">${item.cpuLoad || 0}%</td>
                            <td class="${setColor100(item.gpuLoad)}">${item.gpuLoad || 0}%</td>
                            <td class="${setColor100(item.cpuTemp)}">${item.cpuTemp || 0}°C</td>
                            <td class="${setColor100(item.gpuTemp)}">${item.gpuTemp || 0}°C</td>
                            <td>${item.cpuClock || 0} MHz</td>
                            <td>${item.gpuClock || 0} MHz</td>
                            <td>${item.cpuPow || 0} W</td>
                            <td>${item.gpuPow || 0} W</td>
                            <td>${item.cpuFan || 0} Rpm</td>
                            <td>${item.gpuFan || 0} Rpm</td>
                            <td class="${setColor100(item.ramUsage)}">${item.ramUsage || 0}%</td>
                            <td>${item.ramSpeed || 0} MHz</td>
                            <td>${item.uploadSpeed || 0} Kbps</td>
                            <td>${item.downloadSpeed || 0} Kbps</td>
                            <td class="${setColor2500(item.lanSpeed)}">${item.lanSpeed || 0} Mbps</td>
                            <td>${item.ping || '0'} ms</td>
                            <td>${item.appRunning || 'N/A'}</td>
                            <td>${item.uptime || 'N/A'}</td>
                        </tr>
                    `;
                    tbody.innerHTML += row;
                });
            } catch (error) {
                console.error('Error fetching WKS data:', error);
            }
        },

        startAutoUpdate: function () {
            console.log('WKS: Starting auto update');
            if (isUpdating) {
                console.log('WKS: Already updating, no action needed');
                return;
            }

            // Đảm bảo dừng cập nhật cũ nếu có
            this.stopAutoUpdate();

            // Đánh dấu trạng thái đang cập nhật
            isUpdating = true;

            // Cập nhật ngay lập tức
            this.updateData();

            // Thiết lập cập nhật định kỳ
            updateInterval = setInterval(() => this.updateData(), 5000);
        },

        stopAutoUpdate: function () {
            if (!isUpdating && !updateInterval) {
                console.log('WKS: Not updating, no action needed');
                return;
            }

            console.log('WKS: Stopping auto update');

            // Dừng đồng hồ cập nhật
            if (updateInterval) {
                clearInterval(updateInterval);
                updateInterval = null;
            }

            // Cập nhật trạng thái
            isUpdating = false;
        }
    };

    // Đảm bảo dừng cập nhật nếu trang bị tắt hoặc thay đổi
    window.addEventListener('beforeunload', () => {
        wksModule.stopAutoUpdate();
    });

    // Gán các phương thức vào window để có thể gọi từ index.js
    window.startAutoUpdate = function () {
        wksModule.startAutoUpdate();
    };

    window.stopAutoUpdate = function () {
        wksModule.stopAutoUpdate();
    };

    // Không tự động khởi động khi script được load
    // Sẽ do index.js gọi window.startAutoUpdate() sau khi script được load hoàn tất
})();