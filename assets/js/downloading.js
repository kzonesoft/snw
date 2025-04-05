// Sử dụng IIFE (Immediately Invoked Function Expression) để tránh xung đột biến toàn cục
(function () {
    let updateInterval = null;
    let isUpdating = false;

    const downloadingModule = {
        updateData: async function () {
            if (!isUpdating) return;

            try {
                const token = sessionStorage.getItem('token'); // Lấy token từ sessionStorage
                if (!token) {
                    window.location.href = '/login'; // Điều hướng đến trang login
                    this.stopAutoUpdate();
                    return;
                }

                document.getElementById('total-games').textContent = '...'; // Hiển thị đang tải

                const response = await fetch('/api/games/downloading', {
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
                    this.stopAutoUpdate();
                    return;
                }

                const data = await response.json();
                const totalGames = data.length;
                document.getElementById('total-games').textContent = totalGames;

                const tbody = document.getElementById('data-body');
                tbody.innerHTML = '';

                data.forEach(item => {
                    // Tính phần trăm tiến độ tải
                    const progressPercent = item.progress !== undefined ?
                        item.progress :
                        (item.size > 0 ?
                            Math.round(((item.size - item.remainingSize) / item.size) * 100) : 0);

                    // Định dạng dung lượng file
                    const formatSize = (sizeInMB) => {
                        if (sizeInMB >= 1024) {
                            return (sizeInMB / 1024).toFixed(2) + ' GB';
                        } else {
                            return sizeInMB.toFixed(2) + ' MB';
                        }
                    };

                    // Xác định màu sắc cho trạng thái
                    const getStatusClass = (status) => {
                        switch (status) {
                            case 'Downloading':
                                return 'status-green';
                            case 'Paused':
                                return 'status-orange';
                            case 'Error':
                                return 'status-red';
                            default:
                                return 'status-blue';
                        }
                    };

                    // Tạo row cho từng game
                    const row = `
                        <tr>
                            <td class="status-blue">${item.name || 'N/A'}</td>
                            <td>${formatSize(item.size || 0)}</td>
                            <td>${formatSize(item.remainingSize || 0)}</td>
                            <td class="${getStatusClass(item.status)}">${item.status || 'N/A'}</td>
                            <td>
                                <div class="progress-bar-container">
                                    <div class="progress-bar" style="width: ${progressPercent}%;"></div>
                                </div>
                                <div style="text-align: center; margin-top: 5px;">${progressPercent}%</div>
                            </td>
                            <td>${item.downloadSpeed?.toFixed(2) || '0'} MB/s</td>
                        </tr>
                    `;
                    tbody.innerHTML += row;
                });
            } catch (error) {
                console.error('Error fetching downloading games info:', error);
            }
        },

        startAutoUpdate: function () {
            console.log('Downloading Games: Starting auto update');
            if (isUpdating) {
                console.log('Downloading Games: Already updating, no action needed');
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
                console.log('Downloading Games: Not updating, no action needed');
                return;
            }

            console.log('Downloading Games: Stopping auto update');

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
        downloadingModule.stopAutoUpdate();
    });

    // Gán các phương thức vào window để có thể gọi từ index.js
    window.startAutoUpdate = function () {
        downloadingModule.startAutoUpdate();
    };

    window.stopAutoUpdate = function () {
        downloadingModule.stopAutoUpdate();
    };

    // Không tự động khởi động khi script được load
    // Sẽ do index.js gọi window.startAutoUpdate() sau khi script được load hoàn tất
})();