// Sử dụng IIFE (Immediately Invoked Function Expression) để tránh xung đột biến toàn cục
(function () {
    // Lưu trữ hành động hiện tại (shutdown hoặc restart)
    let currentAction = null;

    // Các phần tử DOM
    const modal = document.getElementById('passwordModal');
    const modalTitle = document.getElementById('modal-title');
    const modalMessage = document.getElementById('modal-message');
    const passwordInput = document.getElementById('password-input');
    const errorMessage = document.getElementById('error-message');
    const closeButton = document.querySelector('.close-button');
    const cancelButton = document.getElementById('cancel-button');
    const confirmButton = document.getElementById('confirm-button');
    const shutdownButton = document.getElementById('shutdown-button');
    const restartButton = document.getElementById('restart-button');

    // Mở modal với hành động tương ứng
    function openModal(action) {
        currentAction = action;

        if (action === 'shutdown') {
            modalTitle.textContent = 'Xác nhận tắt máy';
            modalMessage.textContent = 'Vui lòng nhập mật khẩu để xác nhận tắt máy chủ:';
        } else if (action === 'restart') {
            modalTitle.textContent = 'Xác nhận khởi động lại';
            modalMessage.textContent = 'Vui lòng nhập mật khẩu để xác nhận khởi động lại máy chủ:';
        }

        passwordInput.value = '';
        errorMessage.style.display = 'none';
        modal.style.display = 'flex';
        passwordInput.focus();
    }

    // Đóng modal
    function closeModal() {
        modal.style.display = 'none';
        currentAction = null;
    }

    async function sendPowerAction() {
        const password = passwordInput.value.trim();
        if (!password) {
            errorMessage.textContent = 'Vui lòng nhập mật khẩu!';
            errorMessage.style.display = 'block';
            errorMessage.style.color = 'red'; // Đảm bảo màu đỏ cho thông báo lỗi
            return;
        }

        try {
            const token = sessionStorage.getItem('token');
            if (!token) {
                window.location.href = '/login';
                return;
            }

            const response = await fetch('/api/server/power', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({
                    Action: currentAction,
                    Password: password
                })
            });

            const data = await response.json();

            if (data.success) {
                // Nếu yêu cầu thành công
                // Thay đổi màu sắc của thông báo thành màu xanh lá để hiển thị thành công
                errorMessage.style.color = 'green';

                // Đặt nội dung thành thông báo thành công
                if (currentAction === 'shutdown') {
                    errorMessage.textContent = 'Máy chủ sẽ tắt trong giây lát!';
                } else if (currentAction === 'restart') {
                    errorMessage.textContent = 'Máy chủ sẽ khởi động lại trong giây lát!';
                }

                // Hiển thị thông báo
                errorMessage.style.display = 'block';

                // Đóng modal sau một khoảng thời gian (ví dụ: 3 giây)
                setTimeout(() => {
                    closeModal();
                }, 5000);

            } else {
                // Nếu có lỗi
                errorMessage.style.color = 'red'; // Đảm bảo màu đỏ cho thông báo lỗi
                errorMessage.textContent = data.message || 'Mật khẩu không đúng hoặc không có quyền thực hiện hành động này.';
                errorMessage.style.display = 'block';
            }
        } catch (error) {
            console.error('Lỗi khi gửi yêu cầu:', error);
            errorMessage.style.color = 'red';
            errorMessage.textContent = 'Có lỗi xảy ra. Vui lòng thử lại sau.';
            errorMessage.style.display = 'block';
        }
    }

    // Đăng ký các event listeners
    shutdownButton.addEventListener('click', () => openModal('shutdown'));
    restartButton.addEventListener('click', () => openModal('restart'));
    closeButton.addEventListener('click', closeModal);
    cancelButton.addEventListener('click', closeModal);
    confirmButton.addEventListener('click', sendPowerAction);

    //// Đóng modal khi click bên ngoài
    //window.addEventListener('click', (event) => {
    //    if (event.target === modal) {
    //        // Hỏi người dùng có muốn đóng modal không
    //        if (confirm('Bạn có muốn hủy thao tác này không?')) {
    //            closeModal();
    //        }
    //    }
    //});

    // Xử lý khi nhấn Enter trong input mật khẩu
    passwordInput.addEventListener('keydown', (event) => {
        if (event.key === 'Enter') {
            sendPowerAction();
        }
    });

    // Xử lý kiểm tra token khi tải trang
    function checkToken() {
        const token = sessionStorage.getItem('token');
        if (!token) {
            window.location.href = '/login';
        }
    }

    // Module power cho index.js
    const powerModule = {
        startAutoUpdate: function () {
            console.log('Power: Kiểm tra token');
            checkToken();
        },

        stopAutoUpdate: function () {
            console.log('Power: Không có auto update để dừng');
        }
    };

    // Gán các phương thức vào window để có thể gọi từ index.js
    window.startAutoUpdate = function () {
        powerModule.startAutoUpdate();
    };

    window.stopAutoUpdate = function () {
        powerModule.stopAutoUpdate();
    };
})();