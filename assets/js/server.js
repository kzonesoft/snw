// Sử dụng IIFE (Immediately Invoked Function Expression) để tránh xung đột biến toàn cục
(function () {
    let cpuChart = null;
    let ramChart = null;
    let updateInterval = null;
    let lastTouchY = 0;
    let preventPullToRefresh = false;
    let isUpdating = false;

    const serverModule = {
        initCharts: function () {
            // Khởi tạo biểu đồ CPU với tùy chọn phản hồi nhanh hơn cho mobile
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
                        duration: window.innerWidth <= 768 ? 200 : 500 // Rút ngắn thời gian animation trên mobile
                    }
                }
            });

            // Khởi tạo biểu đồ RAM với tùy chọn tương tự
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
                        duration: window.innerWidth <= 768 ? 200 : 500
                    }
                }
            });
        },

        loadData: async function () {
            if (isUpdating) return; // Ngăn chặn nhiều yêu cầu đồng thời

            isUpdating = true;

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
                // Trong trường hợp lỗi, giữ nguyên dữ liệu cũ không thay đổi UI
            } finally {
                isUpdating = false;
            }
        },

        updateDiskList: function (drives) {
            const diskList = document.getElementById('diskList');
            diskList.innerHTML = '';

            // Sắp xếp ổ đĩa theo tên
            drives.sort((a, b) => a.disk.localeCompare(b.disk));

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
                diskItem.setAttribute('data-drive', drive.disk);
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
            console.log('Server Stats: Starting auto update');

            // Khởi tạo biểu đồ
            this.initCharts();

            // Tối ưu cho thiết bị di động
            optimizeForMobile();

            // Tải dữ liệu ngay lập tức
            this.loadData();

            // Thiết lập cập nhật định kỳ - mỗi 30 giây
            clearInterval(updateInterval); // Xóa interval cũ (nếu có)
            updateInterval = setInterval(() => this.loadData(), 30000);
        },

        stopAutoUpdate: function () {
            console.log('Server Stats: Stopping auto update');

            // Dừng cập nhật tự động
            if (updateInterval) {
                clearInterval(updateInterval);
                updateInterval = null;
            }
        }
    };

    // Tối ưu scroll trên thiết bị di động
    function optimizeForMobile() {
        if (window.innerWidth <= 768) {
            // Ngăn chặn việc bounce scroll trên iOS
            document.body.addEventListener('touchmove', function (e) {
                if (e.target.closest('.disk-list, .charts-container')) {
                    e.stopPropagation();
                }
            }, { passive: true });

            // Đảm bảo rằng disk-list không cao quá màn hình
            const diskList = document.getElementById('diskList');
            if (diskList) {
                const maxHeight = window.innerHeight * 0.6; // 60% chiều cao màn hình
                diskList.style.maxHeight = maxHeight + 'px';
                diskList.style.overflowY = 'auto';
            }

            // Xử lý pull-to-refresh trên iOS
            document.addEventListener('touchstart', function (e) {
                if (window.scrollY === 0) {
                    lastTouchY = e.touches[0].clientY;
                    preventPullToRefresh = true;
                } else {
                    preventPullToRefresh = false;
                }
            }, { passive: false });

            document.addEventListener('touchmove', function (e) {
                if (preventPullToRefresh && e.touches[0].clientY > lastTouchY && window.scrollY === 0) {
                    e.preventDefault();
                }
            }, { passive: false });
        }
    }

    // Xử lý khi resize màn hình
    window.addEventListener('resize', function () {
        optimizeForMobile();
    });

    // Gán các phương thức vào window để có thể gọi từ index.js
    window.startAutoUpdate = function () {
        serverModule.startAutoUpdate();
    };

    window.stopAutoUpdate = function () {
        serverModule.stopAutoUpdate();
    };

    // Khi trang được tải xong, đảm bảo tốc độ cuộn mượt mà
    window.addEventListener('DOMContentLoaded', function () {
        // Kích hoạt fastclick để giảm độ trễ trên thiết bị di động
        document.body.addEventListener('touchstart', function () { }, { passive: true });
    });
})();