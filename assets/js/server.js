// Sử dụng IIFE (Immediately Invoked Function Expression) để tránh xung đột biến toàn cục
(function () {
    let cpuChart = null;
    let ramChart = null;

    const serverModule = {
        initCharts: function () {
            // Khởi tạo biểu đồ CPU
            const cpuCtx = document.getElementById('cpuChart').getContext('2d');
            cpuChart = new Chart(cpuCtx, {
                type: 'doughnut',
                data: {
                    labels: ['Sử dụng', 'Còn trống'],
                    datasets: [{
                        data: [0, 100],
                        backgroundColor: ['#4171AF', '#E2E8F0'],
                        borderWidth: 0,
                        cutout: '75%'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            display: false
                        },
                        tooltip: {
                            enabled: false
                        }
                    },
                    animation: {
                        duration: 500
                    }
                }
            });

            // Khởi tạo biểu đồ RAM
            const ramCtx = document.getElementById('ramChart').getContext('2d');
            ramChart = new Chart(ramCtx, {
                type: 'doughnut',
                data: {
                    labels: ['Sử dụng', 'Còn trống'],
                    datasets: [{
                        data: [0, 100],
                        backgroundColor: ['#48bb78', '#E2E8F0'],
                        borderWidth: 0,
                        cutout: '75%'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            display: false
                        },
                        tooltip: {
                            enabled: false
                        }
                    },
                    animation: {
                        duration: 500
                    }
                }
            });
        },

        loadData: async function () {
            try {
                const token = sessionStorage.getItem('token'); // Lấy token từ sessionStorage
                if (!token) {
                    window.location.href = '/login'; // Điều hướng đến trang login
                    return;
                }

                const response = await fetch('/api/server/statistic', {
                    method: 'GET',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${token}`,
                    },
                });

                if (response.status === 401) {
                    // Nếu response là 401, xóa token và làm mới trang
                    sessionStorage.removeItem('token');
                    window.location.href = '/login';
                    return;
                }

                const data = await response.json();

                // Cập nhật biểu đồ CPU
                cpuChart.data.datasets[0].data = [data.cpuLoad, 100 - data.cpuLoad];
                cpuChart.update();
                document.getElementById('cpuPercentage').textContent = `${data.cpuLoad}%`;

                // Cập nhật biểu đồ RAM
                ramChart.data.datasets[0].data = [data.ramLoad, 100 - data.ramLoad];
                ramChart.update();
                document.getElementById('ramPercentage').textContent = `${data.ramLoad}%`;

                // Cập nhật danh sách ổ đĩa
                this.updateDiskList(data.drives);

            } catch (error) {
                console.error('Error fetching server statistics:', error);
            }
        },

        updateDiskList: function (drives) {
            const diskList = document.getElementById('diskList');
            diskList.innerHTML = '';

            drives.forEach(drive => {
                // Tính phần trăm sử dụng
                const usedSize = drive.totalSize - drive.freeSize;
                const usagePercent = ((usedSize / drive.totalSize) * 100).toFixed(1);

                // Định dạng dung lượng
                const formatSize = (sizeInMB) => {
                    if (sizeInMB >= 1024) {
                        return (sizeInMB / 1024).toFixed(2) + ' GB';
                    } else {
                        return sizeInMB + ' MB';
                    }
                };

                // Xác định màu sắc dựa trên mức sử dụng
                let usageClass = 'usage-low';
                if (usagePercent > 85) {
                    usageClass = 'usage-high';
                } else if (usagePercent > 70) {
                    usageClass = 'usage-medium';
                }

                // Tạo HTML cho ổ đĩa
                const diskItem = document.createElement('div');
                diskItem.className = 'disk-item';
                diskItem.innerHTML = `
                    <div class="disk-info">
                        <div class="disk-icon">
                            <i class="fas fa-hdd"></i>
                        </div>
                        <div class="disk-details">
                            <div class="disk-name">Ổ đĩa ${drive.disk}</div>
                            <div class="disk-space">${formatSize(usedSize)} / ${formatSize(drive.totalSize)}</div>
                        </div>
                    </div>
                    <div class="progress-container">
                        <div class="progress-bar ${usageClass}" style="width: ${usagePercent}%"></div>
                    </div>
                    <div class="progress-info">
                        <span>Đã sử dụng: ${usagePercent}%</span>
                        <span>Còn trống: ${formatSize(drive.freeSize)}</span>
                    </div>
                `;

                diskList.appendChild(diskItem);
            });
        },

        startAutoUpdate: function () {
            console.log('Server Stats: Loading data once');

            // Khởi tạo biểu đồ
            this.initCharts();

            // Chỉ tải dữ liệu 1 lần khi trang được tải
            this.loadData();
        },

        stopAutoUpdate: function () {
            console.log('Server Stats: No interval to stop');
            // Không cần làm gì vì không có interval
        }
    };

    // Gán các phương thức vào window để có thể gọi từ index.js
    window.startAutoUpdate = function () {
        serverModule.startAutoUpdate();
    };

    window.stopAutoUpdate = function () {
        serverModule.stopAutoUpdate();
    };

    // Thêm nút refresh
    document.addEventListener('DOMContentLoaded', function () {
        // Tạo nút refresh 
        const headerCard = document.querySelector('.header-card');
        const refreshButton = document.createElement('button');
        refreshButton.className = 'refresh-button';
        refreshButton.innerHTML = '<i class="fas fa-sync-alt"></i> Làm mới';
        refreshButton.style.background = 'transparent';
        refreshButton.style.border = '1px solid #98FB98';
        refreshButton.style.color = '#98FB98';
        refreshButton.style.padding = '4px 8px';
        refreshButton.style.borderRadius = '3px';
        refreshButton.style.fontSize = '0.8rem';
        refreshButton.style.cursor = 'pointer';
        refreshButton.style.marginLeft = '10px';
        refreshButton.addEventListener('click', function () {
            serverModule.loadData();
        });
        headerCard.appendChild(refreshButton);
    });
})();