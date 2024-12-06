import {useNavigate} from "react-router-dom";
import {useEffect, useRef, useState} from "react";
import { DataTable } from 'primereact/datatable';
import { Column } from 'primereact/column';
import {isNullOrUndef} from "chart.js/helpers";
import {Toast} from "primereact/toast";
import { ConfirmDialog } from 'primereact/confirmdialog';
import { ProgressSpinner } from 'primereact/progressspinner';
import { TabMenu } from 'primereact/tabmenu';
import {Editor} from "primereact/editor";
import {Dialog} from "primereact/dialog";
import { Tag } from 'primereact/tag';

export default function ShowHome(){
    // диалоговое окно для ответа на запрос дружбы
    const [visibleFriendCoinf, setVisibleFriendCoinf] = useState(false);

    // диалоговое окно для чтения письма
    const [visibleLetterDialog, setVisibleLetterDialog] = useState(false);

    // показываем пользователю, что письма загружаются (выводим ProgressSpinner)
    const [showSkeleton, setShowSkeleton] = useState(true);

    // для уведомлений Toast
    const toast = useRef(null);

    // письма из ящика
    const [letters, setLetters] = useState([]);

    // выбранное письмо
    const [selectedLetter, setSelectedLetter] = useState(null);

    // информация о письме, пришедшая с сервера
    const [letterInfo, setLetterInfo] = useState(null);

    // отправленный запрос дружбы
    const [friendSender, setFriendSender] = useState(null);

    // текущая открытая папка
    const [curFolder, setCurFolder] = useState('Input');

    // сообщение окна подтверждения
    const [coinfMessage, setCoinfMessage] = useState('');

    const navigate = useNavigate();

    const [activeIndex, setActiveIndex] = useState(0);

    const checkUser = () => {
        if(isNullOrUndef(localStorage.getItem('userCredentials'))) {
            navigate('/login');
        }
    }

    // если пользователь не зашёл в аккаунт заставляем его зайти
    useEffect(() => {
        checkUser();
    }, []);

    // получаем письма из ящика с указанной папки
    const getLettersByCommand = async (folder) => {
        const credentialsJSON = localStorage.getItem('userCredentials');
        const data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

        const index = parseInt(localStorage.getItem('curUser'));

        const curUser = data[index];

        setShowSkeleton(true);

        try {
            const response = await fetch("http://localhost:5113/mail/GetMails", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify({
                    Email: curUser.email,
                    AppPassword: curUser.password,
                    Action: folder,
                }),
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const data = await response.json();
            setLetters(data);
            setCurFolder(folder);
            setShowSkeleton(false);
        } catch (error) {
            console.error("Ошибка:", error);
            toast.current.show({
                severity: 'error',
                summary: 'Ошибка',
                detail: 'Что-то пошло не так :('
            });
        }
    };

    const items = [
        {
            label: 'Входящие',
            icon: 'pi pi-inbox',
            command: () => {
                getLettersByCommand('Input');
            }
        },
        {
            label: 'Отправленные',
            icon: 'pi pi-send',
            command: () => {
                getLettersByCommand('Output');
            }
        },
        {
            label: 'Черновики',
            icon: 'pi pi-book',
            command: () => {
                getLettersByCommand('Drafts');
            }
        },
        {
            label: 'Корзина',
            icon: 'pi pi-trash',
            command: () => {
                getLettersByCommand('Trash');
            }
        }
    ];

    useEffect(() => {
        !isNullOrUndef(localStorage.getItem('userCredentials')) && getLettersByCommand('Input');
    }, []);

    useEffect(() =>{
        // проверить есть ли для пользователя запросы дружбы
        if(!isNullOrUndef(localStorage.getItem('userCredentials'))) {
            const credentialsJSON = localStorage.getItem('userCredentials');

            let data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

            const index = parseInt(localStorage.getItem('curUser'));

            checkRequest(data[index]?.email);
        }
    },[]);

    // проверка запросов на дружбу для пользователя
    const checkRequest = async (user) => {
        try {
            const response = await fetch("http://localhost:5113/friends/FindRequestForUser", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify(user),
            });

            if (!response.ok) {
                if (response.status === 400) {
                    const errorData = await response.text();
                    const errorMessage = errorData || 'Неизвестная ошибка при отправке запроса';
                    throw new Error(errorMessage);
                } else {
                    const text = await response.text();
                    throw new Error(`HTTP error! status: ${response.status}, message: ${text}`);
                }
            }
            const data = await response.json();

            console.log(data);
            if(typeof data !== 'string'){
                setFriendSender(data);

                console.log(friendSender);
            }
        } catch (error) {
            console.error("Ошибка:", error);
            toast.current.show({
                severity: 'error',
                summary: 'Ошибка',
                detail: error.message
            });
        }
    }

    useEffect(() => {
        if(!isNullOrUndef(friendSender)) {
            setCoinfMessage(`Пользователь ${friendSender.userFrom} отправил вам запрос дружбы. Принять?`);
            setVisibleFriendCoinf(true);
        }
    }, [friendSender]);

    // отправка на сервер ответа пользователя на запрос
    const answerOnFriendRequest = async (answer) => {
        try {
            const credentialsJSON = localStorage.getItem('userCredentials');

            let users = credentialsJSON ? JSON.parse(credentialsJSON) : [];

            const index = parseInt(localStorage.getItem('curUser'));

            const response = await fetch("http://localhost:5113/friends/AnswerFriendRequest", {
                method: "DELETE",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify({
                    RequestId: friendSender.id,
                    EmailFrom: friendSender.userFrom,
                    EmailTo: users[index]?.email,
                    Answer: answer
                }),
            });

            if (!response.ok) {
                if (response.status === 400) {
                    const errorData = await response.text();
                    const errorMessage = errorData || 'Неизвестная ошибка при отправке запроса';
                    throw new Error(errorMessage);
                } else {
                    const text = await response.text();
                    throw new Error(`HTTP error! status: ${response.status}, message: ${text}`);
                }
            }
            const data = await response.text();

            // Перезагружаем страницу т.к. для пользователя может быть не 1 запрос
            window.location.reload();
        } catch (error) {
            console.error("Ошибка:", error);
            toast.current.show({
                severity: 'error',
                summary: 'Ошибка',
                detail: error.message
            });
        }
    }

    const showSkeletonOrData = () => {
        if (showSkeleton) {
            return(
                <div className="flex align-items-center justify-content-center"
                    style={{width:"100%", height:"75vh"}}>
                    <ProgressSpinner />
                </div>
                );
        }
        else{
            return (
                <DataTable value={letters}
                           tableStyle={{minWidth: '60rem'}}
                           style={{width: '100%'}}
                           emptyMessage={"Папка пуста :("}
                           stripedRows
                           selectionMode="single"
                           selection={selectedLetter}
                           onSelectionChange={(e) => setSelectedLetter(e.value)} dataKey="id"
                           onRowSelect={onRowSelect}
                >
                    <Column field="from" header="Отправитель"></Column>
                    <Column field="subject" header="Тема"></Column>
                    <Column field="date" header="Дата"></Column>
                </DataTable>);
        }
    }

    // проверка начинается ли строка с подстроки
    function startsWithString(str, subStr) {
        if (subStr.length > str.length) {
            return false; // Подстрока длиннее исходной строки
        }
        return str.slice(0, subStr.length) === subStr;
    }

    // действие при нажатии на письмо в списке(открытие письма)
    const onRowSelect = (event) => {
        const credentialsJSON = localStorage.getItem('userCredentials');

        let data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

        const index = parseInt(localStorage.getItem('curUser'));

        fetch("http://localhost:5113/mail/GetLetter", {
            method: "POST",
            headers: {
                "Content-Type": "application/json", // Тип содержимого
            },
            body: JSON.stringify({
                Email: data[index]?.email,
                AppPassword: data[index]?.password,
                Action: curFolder,
                LetterId: event.data.id
            }),
        })
            .then(response => {
                // Обработка ответа от сервера
                if (!response.ok) {
                    // Проверка на ошибки HTTP (4xx или 5xx)
                    // Создаём ошибку, если ответ не успешный.
                    throw new Error(`HTTP error! status: ${response.status}`);
                }
                return response.json();
            })
            .then(data => {
                if(typeof data === 'string' && startsWithString(data, 'Ошибка')) {
                    throw new Error(`Error! ${data}`);
                }
                setLetterInfo(data);
                setVisibleLetterDialog(true);
            })
            .catch(error => {
                // Обработка ошибок
                console.error("Ошибка:", error);
                toast.current.show({
                    severity: 'error',
                    summary: 'Ошибка',
                    detail: 'Что-то пошло не так :('
                });
            });
    };

    const letterFooter = (
        <Tag className="text-base"
             style={{height: '30px'}}
             icon="pi pi-check"
             severity="success"
             value="Подписан">
        </Tag>
    );

    return(
        <div>
            <TabMenu model={items}
                     activeIndex={activeIndex}
                     onTabChange={(e) => setActiveIndex(e.index)} />

            {showSkeletonOrData()}

            <Dialog header={letterInfo?.subject}
                    visible={visibleLetterDialog}
                    style={{ width: '60vw' }}
                    onHide={() => {if (!visibleLetterDialog) return; setVisibleLetterDialog(false); }}
                    footer={letterFooter}
            >
                <div className="card">
                    <p>
                        Отправитель: {letterInfo?.from}
                    </p>
                    <Editor value={letterInfo?.textBody}
                            readOnly
                            style={{height: '320px'}}
                            headerTemplate={<></>}
                    />

                </div>
            </Dialog>

            <ConfirmDialog group="declarative"
                           visible={visibleFriendCoinf}
                           onHide={() => setVisibleFriendCoinf(false)}
                           message={coinfMessage}
                           header="Подтверждение"
                           icon="pi pi-question-circle"
                           closable={false}
                           acceptLabel='Да'
                           rejectLabel='Нет'
                           accept={() => {
                               answerOnFriendRequest(true);
                           }}
                           reject={() => {
                               answerOnFriendRequest(false);
                           }} />
            <Toast ref={toast} />
        </div>
    );
}